using System.Net;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;

namespace SierraNueva.Infrastructure.Discovery;

public sealed class OpportunityFeedReader(
    IHttpClientFactory httpClientFactory,
    OpportunityFeedParser parser) : IOpportunityFeedReader
{
    private const int MaximumDocumentBytes = 64 * 1024 * 1024;
    private const int MaximumArchiveBytes = 512 * 1024 * 1024;
    private const int MaximumSitemapDocuments = 50;
    private const int MaximumSitemapDepth = 2;

    public async Task<IReadOnlyList<OpportunityFeedItem>> ReadAsync(
        OpportunitySourceDefinition source,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(source.FixturePath))
        {
            byte[] fixture = await File.ReadAllBytesAsync(
                source.FixturePath,
                cancellationToken);
            IReadOnlyList<(Uri Uri, DateOnly Date)> fixtureUris =
                BuildUris(source, toDate, toDate);
            Uri baseUri = fixtureUris.Count > 0
                ? fixtureUris[0].Uri
                : new Uri("https://fixtures.invalid/");
            IReadOnlyList<OpportunityFeedItem> fixtureItems =
                parser.Parse(source, fixture, baseUri, toDate);
            return source.Format is
                OpportunityFeedFormat.Sitemap or OpportunityFeedFormat.HtmlLinks
                ? FilterCommercialItems(source, fixtureItems)
                : fixtureItems;
        }

        List<OpportunityFeedItem> items = [];
        foreach ((Uri uri, DateOnly date) in BuildUris(source, fromDate, toDate))
        {
            ValidateUri(source, uri);
            if (source.Format == OpportunityFeedFormat.ZipAtom)
            {
                IReadOnlyList<OpportunityFeedItem>? archiveItems =
                    await DownloadAndParseArchiveAsync(source, uri, cancellationToken);
                if (archiveItems is not null)
                {
                    items.AddRange(archiveItems);
                }

                if (items.Count >= source.MaxItems)
                {
                    break;
                }

                continue;
            }

            byte[]? content = await DownloadAsync(uri, cancellationToken);
            if (content is null)
            {
                continue;
            }

            if (source.Format == OpportunityFeedFormat.BocmCalendar)
            {
                Uri? summaryUri = parser.FindBocmSummaryUri(content, uri);
                if (summaryUri is null)
                {
                    continue;
                }

                ValidateUri(source, summaryUri);
                byte[]? summary = await DownloadAsync(summaryUri, cancellationToken);
                if (summary is null)
                {
                    continue;
                }

                items.AddRange(parser.Parse(source, summary, summaryUri, date));
                if (items.Count >= source.MaxItems)
                {
                    break;
                }

                continue;
            }

            if (source.Format == OpportunityFeedFormat.Sitemap)
            {
                IReadOnlyList<OpportunityFeedItem> sitemapItems = await ReadSitemapAsync(
                    source,
                    uri,
                    content,
                    date,
                    cancellationToken);
                items.AddRange(source.FollowDetailPages
                    ? await ReadDetailPagesAsync(
                        source,
                        sitemapItems,
                        cancellationToken)
                    : sitemapItems);
                if (items.Count >= source.MaxItems)
                {
                    break;
                }

                continue;
            }

            IReadOnlyList<OpportunityFeedItem> parsed = parser.Parse(
                source,
                content,
                uri,
                date);
            IReadOnlyList<OpportunityFeedItem> filtered = source.Format is
                OpportunityFeedFormat.Sitemap or OpportunityFeedFormat.HtmlLinks
                ? FilterCommercialItems(source, parsed)
                : parsed;
            items.AddRange(source.FollowDetailPages
                ? await ReadDetailPagesAsync(
                    source,
                    filtered,
                    cancellationToken)
                : filtered);
            if (items.Count >= source.MaxItems)
            {
                break;
            }
        }

        return items
            .DistinctBy(
                item => string.IsNullOrWhiteSpace(item.ExternalId)
                    ? item.OfficialUrl
                    : item.ExternalId,
                StringComparer.OrdinalIgnoreCase)
            .Take(source.MaxItems)
            .ToArray();
    }

    private async Task<IReadOnlyList<OpportunityFeedItem>> ReadDetailPagesAsync(
        OpportunitySourceDefinition source,
        IEnumerable<OpportunityFeedItem> sitemapItems,
        CancellationToken cancellationToken)
    {
        List<OpportunityFeedItem> details = [];
        foreach (OpportunityFeedItem sitemapItem in sitemapItems
                     .Where(item =>
                         Uri.TryCreate(
                             item.OfficialUrl,
                             UriKind.Absolute,
                             out Uri? uri) &&
                         source.DetailUrlIncludes.Any(pattern =>
                             uri.AbsolutePath.Contains(
                                 pattern,
                                 StringComparison.OrdinalIgnoreCase)))
                     .Take(source.MaxDetailPages))
        {
            Uri detailUri = new(sitemapItem.OfficialUrl);
            ValidateUri(source, detailUri);
            byte[]? content = await DownloadAsync(detailUri, cancellationToken);
            if (content is null)
            {
                continue;
            }

            details.Add(parser.ParseDetailPage(
                source,
                content,
                detailUri,
                sitemapItem.PublishedAtUtc));
        }

        return details;
    }

    private async Task<IReadOnlyList<OpportunityFeedItem>> ReadSitemapAsync(
        OpportunitySourceDefinition source,
        Uri rootUri,
        byte[] rootContent,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        Queue<(Uri Uri, byte[] Content, int Depth)> pending = new();
        pending.Enqueue((rootUri, rootContent, 0));
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        List<OpportunityFeedItem> items = [];
        int documentsRead = 0;

        while (pending.Count > 0 &&
               documentsRead < MaximumSitemapDocuments &&
               items.Count < source.MaxItems)
        {
            (Uri uri, byte[] content, int depth) = pending.Dequeue();
            if (!visited.Add(uri.AbsoluteUri))
            {
                continue;
            }

            documentsRead++;
            IReadOnlyList<Uri>? childUris = parser.FindSitemapIndexUris(content, uri);
            if (childUris is null)
            {
                IReadOnlyList<OpportunityFeedItem> parsed = parser.Parse(
                    source,
                    content,
                    uri,
                    date);
                items.AddRange(FilterCommercialItems(source, parsed));
                continue;
            }

            if (depth >= MaximumSitemapDepth)
            {
                throw new InvalidDataException(
                    $"El índice sitemap '{rootUri}' supera la profundidad máxima " +
                    $"{MaximumSitemapDepth}.");
            }

            foreach (Uri childUri in childUris
                         .Where(uri => IsAllowedSitemapUri(source, uri))
                         .Where(uri => source.SitemapIncludes.Count == 0 ||
                                       source.SitemapIncludes.Any(pattern =>
                                           uri.AbsoluteUri.Contains(
                                               pattern,
                                               StringComparison.OrdinalIgnoreCase)))
                         .Take(MaximumSitemapDocuments - documentsRead - pending.Count))
            {
                byte[]? childContent = await DownloadAsync(childUri, cancellationToken);
                if (childContent is not null)
                {
                    pending.Enqueue((childUri, childContent, depth + 1));
                }
            }
        }

        return items
            .DistinctBy(
                item => string.IsNullOrWhiteSpace(item.ExternalId)
                    ? item.OfficialUrl
                    : item.ExternalId,
                StringComparer.OrdinalIgnoreCase)
            .Take(source.MaxItems)
            .ToArray();
    }

    private static bool IsAllowedSitemapUri(
        OpportunitySourceDefinition source,
        Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps &&
               source.AllowedHosts.Contains(
                   uri.IdnHost,
                   StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<OpportunityFeedItem> FilterCommercialItems(
        OpportunitySourceDefinition source,
        IEnumerable<OpportunityFeedItem> items)
    {
        return items.Where(item =>
                Uri.TryCreate(item.OfficialUrl, UriKind.Absolute, out Uri? uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                source.AllowedHosts.Contains(
                    uri.IdnHost,
                    StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private async Task<IReadOnlyList<OpportunityFeedItem>?> DownloadAndParseArchiveAsync(
        OpportunitySourceDefinition source,
        Uri uri,
        CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("opportunity-discovery");
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/zip");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            !mediaType.Contains("zip", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals(
                "application/octet-stream",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"El archivo '{uri}' debía ser ZIP, pero el servidor devolvió " +
                $"'{mediaType}'.");
        }

        if (response.Content.Headers.ContentLength > MaximumArchiveBytes)
        {
            throw new InvalidDataException(
                $"El archivo '{uri}' supera el límite de {MaximumArchiveBytes} bytes.");
        }

        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"sierranueva-pcsp-{Guid.NewGuid():N}.zip.tmp");
        try
        {
            await using (Stream input =
                         await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (FileStream output = new(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    if (output.Length + read > MaximumArchiveBytes)
                    {
                        throw new InvalidDataException(
                            $"El archivo '{uri}' supera el límite de " +
                            $"{MaximumArchiveBytes} bytes.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            await using FileStream archive = File.OpenRead(temporary);
            return parser.ParseZipArchive(archive, uri, source.MaxItems);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private async Task<byte[]?> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("opportunity-discovery");
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd(
            uri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? "application/zip"
                : "application/json, application/xml, application/atom+xml, text/html");
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"El documento '{uri}' supera el límite de {MaximumDocumentBytes} bytes.");
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream output = new();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > MaximumDocumentBytes)
            {
                throw new InvalidDataException(
                    $"El documento '{uri}' supera el límite de {MaximumDocumentBytes} bytes.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }

    private static IReadOnlyList<(Uri Uri, DateOnly Date)> BuildUris(
        OpportunitySourceDefinition source,
        DateOnly from,
        DateOnly to)
    {
        if (string.IsNullOrWhiteSpace(source.UrlTemplate))
        {
            return [];
        }

        List<DateOnly> dates = source.Cadence switch
        {
            OpportunityFeedCadence.Daily => EnumerateDays(from, to).ToList(),
            OpportunityFeedCadence.Monthly => EnumerateMonths(from, to).ToList(),
            _ => [to]
        };
        return dates
            .Select(date =>
            {
                string url = source.UrlTemplate
                    .Replace(
                        "{date:yyyyMMdd}",
                        date.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    .Replace(
                        "{date:yyyyMM}",
                        date.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    .Replace(
                        "{date:dd%2FMM%2Fyyyy}",
                        date.ToString(
                                "dd/MM/yyyy",
                                System.Globalization.CultureInfo.InvariantCulture)
                            .Replace("/", "%2F", StringComparison.Ordinal),
                        StringComparison.Ordinal);
                return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                    ? (Uri: uri, Date: date)
                    : throw new InvalidDataException(
                        $"La fuente '{source.Id}' genera una URL inválida.");
            })
            .DistinctBy(item => item.Uri.AbsoluteUri, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<DateOnly> EnumerateDays(DateOnly from, DateOnly to)
    {
        for (DateOnly date = from; date <= to; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static IEnumerable<DateOnly> EnumerateMonths(DateOnly from, DateOnly to)
    {
        DateOnly first = new(from.Year, from.Month, 1);
        DateOnly last = new(to.Year, to.Month, 1);
        for (DateOnly date = first; date <= last; date = date.AddMonths(1))
        {
            yield return date;
        }
    }

    private static void ValidateUri(OpportunitySourceDefinition source, Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException(
                $"La fuente de radar '{source.Id}' debe usar HTTPS.");
        }

        if (source.AllowedHosts.Count == 0 ||
            !source.AllowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"El host '{uri.IdnHost}' no está permitido para '{source.Id}'.");
        }
    }
}
