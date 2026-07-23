using System.Xml;
using System.Xml.Linq;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Infrastructure.Crawling;

namespace SierraNueva.Infrastructure.Discovery;

public sealed class SitemapDiscoveryProvider(IHttpClientFactory httpClientFactory) : IUrlDiscoveryProvider
{
    private static readonly string[] RelevantTerms =
    [
        "promocion",
        "obra-nueva",
        "obra_nueva",
        "residencial",
        "viviendas",
        "chalets",
        "adosados",
        "pareados",
        "unifamiliares",
        "proyectos",
        "cooperativa"
    ];

    public async Task<IReadOnlyList<Uri>> DiscoverAsync(
        SourceDefinition source,
        CancellationToken cancellationToken)
    {
        if (!source.UseSitemaps || !Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            return [];
        }

        HttpClient client = httpClientFactory.CreateClient("crawler");
        Queue<Uri> pending = new();
        HashSet<string> visited = new(StringComparer.Ordinal);
        List<Uri> result = [];

        if (source.UseRobots)
        {
            try
            {
                Uri robotsUri = new(baseUri, "/robots.txt");
                string robotsText = await client.GetStringAsync(robotsUri, cancellationToken);
                foreach (Uri sitemap in RobotsRules.Parse(robotsText, baseUri).Sitemaps)
                {
                    pending.Enqueue(sitemap);
                }
            }
            catch (HttpRequestException)
            {
                // A missing robots file does not imply a sitemap is unavailable.
            }
        }

        pending.Enqueue(new Uri(baseUri, "/sitemap.xml"));
        int sitemapCount = 0;
        while (pending.Count > 0 && sitemapCount < 10 && result.Count < source.MaxPages * 4)
        {
            Uri sitemap = pending.Dequeue();
            if (!visited.Add(sitemap.AbsoluteUri))
            {
                continue;
            }

            sitemapCount++;
            try
            {
                string xml = await client.GetStringAsync(sitemap, cancellationToken);
                foreach (Uri location in ReadLocations(xml, sitemap))
                {
                    if (location.AbsolutePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Enqueue(location);
                    }
                    else if (IsRelevant(location, source))
                    {
                        result.Add(location);
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Discovery is opportunistic; configured URLs remain usable.
            }
            catch (XmlException)
            {
                // Malformed third-party sitemaps are ignored.
            }
        }

        return result
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.Ordinal)
            .OrderBy(uri => uri.AbsoluteUri, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<Uri> ReadLocations(string xml, Uri baseUri)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using StringReader stringReader = new(xml);
        using XmlReader reader = XmlReader.Create(stringReader, settings);
        XDocument document = XDocument.Load(reader);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "loc")
            .Select(element => Uri.TryCreate(baseUri, element.Value.Trim(), out Uri? uri) ? uri : null)
            .Where(uri => uri is not null)
            .Cast<Uri>()
            .ToArray();
    }

    private static bool IsRelevant(Uri uri, SourceDefinition source)
    {
        string value = Uri.UnescapeDataString(uri.AbsolutePath).ToLowerInvariant();
        bool included = source.IncludePatterns.Count == 0 ||
                        source.IncludePatterns.Any(pattern =>
                            value.Contains(pattern, StringComparison.OrdinalIgnoreCase)) ||
                        RelevantTerms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
        bool excluded = source.ExcludePatterns.Any(pattern =>
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        return included && !excluded;
    }
}
