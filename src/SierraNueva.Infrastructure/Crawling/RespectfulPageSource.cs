using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;
using SierraNueva.Infrastructure.Discovery;

namespace SierraNueva.Infrastructure.Crawling;

public sealed class RespectfulPageSource(
    IHttpClientFactory httpClientFactory,
    IEnumerable<IUrlDiscoveryProvider> discoveryProviders,
    IUrlPolicy urlPolicy,
    InternalLinkDiscoveryProvider internalLinkDiscovery,
    IDynamicPageRenderer dynamicPageRenderer,
    IPdfTextExtractor pdfTextExtractor,
    FileHttpMetadataCache metadataCache,
    ILogger<RespectfulPageSource> logger) : IPageSource
{
    private static readonly Action<ILogger, Uri, SkipReason, Exception?> LogDiscardedUrl =
        LoggerMessage.Define<Uri, SkipReason>(
            LogLevel.Warning,
            new EventId(1001, "DiscardedUrl"),
            "URL descartada {Url} por {Reason}");

    private static readonly Action<ILogger, string, Exception?> LogRobotsUnavailable =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1002, "RobotsUnavailable"),
            "No se pudo leer robots.txt de {Host}");

    private readonly HtmlParser _parser = new();
    private readonly Dictionary<string, DateTimeOffset> _lastRequestByHost =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<PageBatch> FetchAsync(
        SourceDefinition source,
        CrawlerSettings settings,
        int? maxPagesOverride,
        bool disablePlaywright,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(source.FixturePath))
        {
            return await ReadFixturesAsync(source, cancellationToken);
        }

        int maxPages = Math.Min(
            maxPagesOverride ?? source.MaxPages,
            settings.MaxPagesPerSource);
        Queue<(Uri Url, int Depth)> pending = new();
        foreach (IUrlDiscoveryProvider provider in discoveryProviders)
        {
            IReadOnlyList<Uri> discovered = await provider.DiscoverAsync(source, cancellationToken);
            foreach (Uri uri in discovered)
            {
                pending.Enqueue((uri, 0));
            }
        }

        HashSet<string> visited = new(StringComparer.Ordinal);
        Dictionary<string, RobotsRules> robotsByHost = new(StringComparer.OrdinalIgnoreCase);
        List<FetchedPage> pages = [];
        List<string> warnings = [];
        int skipped = 0;
        int discoveredCount = pending.Count;

        while (pending.Count > 0 && pages.Count < maxPages)
        {
            (Uri url, int depth) = pending.Dequeue();
            string normalized = UrlNormalizer.Normalize(url.AbsoluteUri);
            if (!visited.Add(normalized))
            {
                skipped++;
                continue;
            }

            if (!urlPolicy.IsAllowed(url, source, out SkipReason reason))
            {
                skipped++;
                warnings.Add($"{reason}: {url}");
                LogDiscardedUrl(logger, url, reason, null);
                continue;
            }

            RobotsRules robots = await GetRobotsAsync(
                url,
                source,
                robotsByHost,
                settings,
                cancellationToken);
            if (source.UseRobots && !robots.IsAllowed(url))
            {
                skipped++;
                warnings.Add($"{SkipReason.RobotsDenied}: {url}");
                continue;
            }

            await EnforceDelayAsync(
                url.Host,
                Math.Max(settings.RequestDelayMilliseconds, source.RequestDelayMilliseconds),
                cancellationToken);
            HttpFetchResult fetch = await FetchAsync(url, settings, cancellationToken);
            if (fetch.NotModified)
            {
                warnings.Add($"HTTP_NOT_MODIFIED: {url}");
                continue;
            }

            if (fetch.Content is null || fetch.ContentType is null)
            {
                skipped++;
                warnings.Add($"{fetch.SkipReason}: {url}");
                continue;
            }

            string content = fetch.Content;
            string contentType = fetch.ContentType;
            if (contentType == "application/pdf")
            {
                byte[] bytes = Convert.FromBase64String(content);
                string pdfText = pdfTextExtractor.Extract(bytes);
                pages.Add(new(url, pdfText, "text/plain", DateTimeOffset.UtcNow, "pdf"));
                continue;
            }

            bool usePlaywright = !disablePlaywright &&
                                 settings.EnablePlaywright &&
                                 source.UsePlaywright &&
                                 StripMarkup(content).Length < 220;
            if (usePlaywright)
            {
                string? rendered = await dynamicPageRenderer.RenderAsync(url, cancellationToken);
                if (!string.IsNullOrWhiteSpace(rendered))
                {
                    content = rendered;
                }
            }

            pages.Add(new(url, content, contentType, DateTimeOffset.UtcNow));

            if (!source.FollowInternalLinks || depth >= Math.Min(source.MaxDepth, settings.MaxDepth))
            {
                continue;
            }

            IDocument document = await _parser.ParseDocumentAsync(content, cancellationToken);
            IReadOnlyList<Uri> links = internalLinkDiscovery.Discover(document, url, source);
            foreach (Uri link in links)
            {
                pending.Enqueue((link, depth + 1));
                discoveredCount++;
            }
        }

        return new()
        {
            Pages = pages,
            DiscoveredUrls = discoveredCount,
            SkippedUrls = skipped,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private async Task<PageBatch> ReadFixturesAsync(
        SourceDefinition source,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(source.FixturePath!, Directory.GetCurrentDirectory());
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"No existe el directorio de fixtures '{directory}'.");
        }

        string[] files = Directory.GetFiles(directory, "*.html", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        List<FetchedPage> pages = [];
        for (int index = 0; index < files.Length; index++)
        {
            string content = await File.ReadAllTextAsync(files[index], cancellationToken);
            IDocument document = await _parser.ParseDocumentAsync(content, cancellationToken);
            string? explicitUrl = document.QuerySelector("meta[name='source-url']")
                ?.GetAttribute("content");
            string fallback = source.StartUrls.ElementAtOrDefault(index) ??
                              $"https://fixtures.sierranueva.test/{Path.GetFileName(files[index])}";
            Uri url = new(explicitUrl ?? fallback);
            pages.Add(new(url, content, "text/html", DateTimeOffset.UtcNow, "fixture"));
        }

        return new()
        {
            Pages = pages,
            DiscoveredUrls = files.Length
        };
    }

    private async Task<RobotsRules> GetRobotsAsync(
        Uri url,
        SourceDefinition source,
        IDictionary<string, RobotsRules> cache,
        CrawlerSettings settings,
        CancellationToken cancellationToken)
    {
        if (!source.UseRobots)
        {
            return RobotsRules.AllowAll;
        }

        if (cache.TryGetValue(url.Host, out RobotsRules? cached))
        {
            return cached;
        }

        try
        {
            Uri robotsUri = new($"{url.Scheme}://{url.Authority}/robots.txt");
            HttpFetchResult result = await FetchAsync(
                robotsUri,
                settings,
                cancellationToken,
                allowTextPlain: true,
                useConditionalRequest: false);
            RobotsRules parsed = result.Content is null
                ? RobotsRules.AllowAll
                : RobotsRules.Parse(result.Content, url);
            cache[url.Host] = parsed;
            return parsed;
        }
        catch (HttpRequestException exception)
        {
            LogRobotsUnavailable(logger, url.Host, exception);
            cache[url.Host] = RobotsRules.AllowAll;
            return RobotsRules.AllowAll;
        }
    }

    private async Task<HttpFetchResult> FetchAsync(
        Uri url,
        CrawlerSettings settings,
        CancellationToken cancellationToken,
        bool allowTextPlain = false,
        bool useConditionalRequest = true)
    {
        HttpClient client = httpClientFactory.CreateClient("crawler");
        for (int attempt = 0; attempt <= settings.MaxRetries; attempt++)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(settings.UserAgent);
            request.Headers.Accept.Add(new("text/html"));
            request.Headers.Accept.Add(new("application/xhtml+xml"));
            request.Headers.Accept.Add(new("application/pdf", 0.6));

            if (useConditionalRequest && metadataCache.Get(url) is { } metadata)
            {
                if (EntityTagHeaderValue.TryParse(metadata.ETag, out EntityTagHeaderValue? tag))
                {
                    request.Headers.IfNoneMatch.Add(tag);
                }

                request.Headers.IfModifiedSince = metadata.LastModifiedUtc;
            }

            try
            {
                using HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new() { NotModified = true };
                }

                if (IsTemporary(response.StatusCode) && attempt < settings.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt)), cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                string mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant()
                                   ?? "application/octet-stream";
                bool allowed = mediaType is "text/html" or "application/xhtml+xml" or "application/pdf" ||
                               (allowTextPlain && mediaType == "text/plain");
                if (!allowed)
                {
                    return new() { SkipReason = SkipReason.InvalidContentType };
                }

                int limit = mediaType == "application/pdf"
                    ? settings.MaxPdfBytes
                    : settings.MaxResponseBytes;
                if (response.Content.Headers.ContentLength > limit)
                {
                    return new() { SkipReason = SkipReason.ResponseTooLarge };
                }

                byte[] bytes = await ReadLimitedAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    limit,
                    cancellationToken);
                await metadataCache.SetAsync(
                    url,
                    response.Headers.ETag?.ToString(),
                    response.Content.Headers.LastModified,
                    cancellationToken);

                return new()
                {
                    ContentType = mediaType,
                    Content = mediaType == "application/pdf"
                        ? Convert.ToBase64String(bytes)
                        : Decode(bytes, response.Content.Headers.ContentType?.CharSet)
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                                                     attempt < settings.MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt)), cancellationToken);
            }
            catch (HttpRequestException) when (attempt < settings.MaxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt)), cancellationToken);
            }
        }

        return new() { SkipReason = SkipReason.FetchFailed };
    }

    private async Task EnforceDelayAsync(
        string host,
        int milliseconds,
        CancellationToken cancellationToken)
    {
        if (_lastRequestByHost.TryGetValue(host, out DateTimeOffset previous))
        {
            TimeSpan remaining = TimeSpan.FromMilliseconds(milliseconds) -
                                 (DateTimeOffset.UtcNow - previous);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }

        _lastRequestByHost[host] = DateTimeOffset.UtcNow;
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream stream,
        int limit,
        CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[16_384];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new InvalidDataException($"La respuesta supera el límite de {limit} bytes.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static string Decode(byte[] bytes, string? charset)
    {
        try
        {
            Encoding encoding = string.IsNullOrWhiteSpace(charset)
                ? Encoding.UTF8
                : Encoding.GetEncoding(charset.Trim('"'));
            return encoding.GetString(bytes);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static bool IsTemporary(HttpStatusCode status)
    {
        return status is HttpStatusCode.RequestTimeout or
               HttpStatusCode.TooManyRequests or
               HttpStatusCode.BadGateway or
               HttpStatusCode.ServiceUnavailable or
               HttpStatusCode.GatewayTimeout;
    }

    private static string StripMarkup(string value)
    {
        StringBuilder builder = new(value.Length);
        bool insideTag = false;
        foreach (char character in value)
        {
            insideTag = character switch
            {
                '<' => true,
                '>' => false,
                _ => insideTag
            };
            if (!insideTag && character != '>')
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Trim();
    }

    private sealed class HttpFetchResult
    {
        public string? Content { get; init; }

        public string? ContentType { get; init; }

        public bool NotModified { get; init; }

        public SkipReason SkipReason { get; init; } = SkipReason.FetchFailed;
    }
}
