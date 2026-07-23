using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Infrastructure.Discovery;

public sealed class OpportunityFeedParser
{
    private const long MaximumArchiveEntryBytes = 64L * 1024 * 1024;
    private readonly HtmlParser _htmlParser = new();

    public IReadOnlyList<OpportunityFeedItem> Parse(
        OpportunitySourceDefinition source,
        ReadOnlyMemory<byte> content,
        Uri sourceUri,
        DateOnly documentDate)
    {
        IReadOnlyList<OpportunityFeedItem> items = source.Format switch
        {
            OpportunityFeedFormat.Rss => ParseRss(Decode(content), sourceUri),
            OpportunityFeedFormat.BoeJson => ParseBoeJson(content, documentDate),
            OpportunityFeedFormat.Atom => ParseAtom(Decode(content), sourceUri),
            OpportunityFeedFormat.ZipAtom => ParseZipAtom(content, sourceUri),
            OpportunityFeedFormat.Html => ParseHtml(
                Decode(content),
                sourceUri,
                source.ItemSelectors),
            OpportunityFeedFormat.BocmCalendar => ParseBocmSummary(
                Decode(content),
                documentDate),
            OpportunityFeedFormat.EAdminHtml => ParseEAdminHtml(
                Decode(content),
                sourceUri),
            OpportunityFeedFormat.Sitemap => ParseSitemap(
                Decode(content),
                sourceUri),
            _ => throw new InvalidDataException(
                $"Formato de radar no admitido: {source.Format}.")
        };

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) &&
                           Uri.TryCreate(item.OfficialUrl, UriKind.Absolute, out _))
            .DistinctBy(
                item => string.IsNullOrWhiteSpace(item.ExternalId)
                    ? item.OfficialUrl
                    : item.ExternalId,
                StringComparer.OrdinalIgnoreCase)
            .Take(source.MaxItems)
            .ToArray();
    }

    public Uri? FindBocmSummaryUri(ReadOnlyMemory<byte> content, Uri sourceUri)
    {
        IDocument document = _htmlParser.ParseDocument(Decode(content));
        return document.QuerySelectorAll("a[href]")
            .Select(element => element.GetAttribute("href"))
            .Where(value =>
                !string.IsNullOrWhiteSpace(value) &&
                value.Contains(
                    "/CM_Boletin_BOCM/",
                    StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(value => ResolveUrl(sourceUri, value!))
            .Where(value => value.Length > 0)
            .Select(value => new Uri(value))
            .FirstOrDefault();
    }

    private static IReadOnlyList<OpportunityFeedItem> ParseRss(string xml, Uri sourceUri)
    {
        XDocument document = ParseXml(xml);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "item")
            .Select(item =>
            {
                string title = ChildValue(item, "title");
                string summary = StripHtml(ChildValue(item, "description"));
                string link = ChildValue(item, "link");
                string guid = ChildValue(item, "guid");
                string resolved = ResolveUrl(sourceUri, link.Length > 0 ? link : guid);
                return new OpportunityFeedItem
                {
                    ExternalId = guid.Length > 0 ? guid : resolved,
                    Title = title,
                    Summary = summary,
                    OfficialUrl = resolved,
                    PublishedAtUtc = ParseDate(ChildValue(item, "pubDate"))
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<OpportunityFeedItem> ParseBoeJson(
        ReadOnlyMemory<byte> content,
        DateOnly documentDate)
    {
        using JsonDocument document = JsonDocument.Parse(content);
        List<OpportunityFeedItem> items = [];
        VisitJson(document.RootElement, documentDate, items);
        return items;
    }

    private static void VisitJson(
        JsonElement element,
        DateOnly documentDate,
        ICollection<OpportunityFeedItem> items)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string identifier = ReadJsonString(element, "identificador");
            string title = ReadJsonString(element, "titulo");
            string url = ReadJsonString(element, "url_html");
            if (identifier.Length > 0 && title.Length > 0 && url.Length > 0)
            {
                items.Add(new()
                {
                    ExternalId = identifier,
                    Title = title,
                    Summary = title,
                    OfficialUrl = url,
                    PublishedAtUtc = new DateTimeOffset(
                        documentDate.ToDateTime(TimeOnly.MinValue),
                        TimeSpan.Zero)
                });
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                VisitJson(property.Value, documentDate, items);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                VisitJson(item, documentDate, items);
            }
        }
    }

    private static IReadOnlyList<OpportunityFeedItem> ParseAtom(string xml, Uri sourceUri)
    {
        XDocument document = ParseXml(xml);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "entry")
            .Select(entry =>
            {
                string id = ChildValue(entry, "id");
                string contractFolderId = entry.Descendants()
                    .FirstOrDefault(element =>
                        element.Name.LocalName == "ContractFolderID")
                    ?.Value
                    ?.Trim() ?? string.Empty;
                string authority = entry.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "author")
                    ?.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName == "name")
                    ?.Value
                    ?.Trim() ?? string.Empty;
                string link = entry.Elements()
                    .FirstOrDefault(element => element.Name.LocalName == "link")
                    ?.Attribute("href")
                    ?.Value ?? string.Empty;
                string title = ChildValue(entry, "title");
                string summary = ChildValue(entry, "summary");
                string context = string.Join(
                    ' ',
                    entry.Descendants()
                        .Where(element => element.Name.LocalName is
                            "CityName" or "CountrySubentity" or "ContractFolderID")
                        .Select(element => element.Value)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                return new OpportunityFeedItem
                {
                    ExternalId = contractFolderId.Length > 0
                        ? $"{authority}|{contractFolderId}"
                        : id,
                    Title = TextNormalizer.CleanEvidence(title, 500),
                    Summary = TextNormalizer.CleanEvidence($"{summary} {context}", 4_000),
                    OfficialUrl = ResolveUrl(
                        sourceUri,
                        link.Length > 0 ? link : id),
                    PublishedAtUtc = ParseDate(
                        ChildValue(entry, "published").Length > 0
                            ? ChildValue(entry, "published")
                            : ChildValue(entry, "updated"))
                };
            })
            .ToArray();
    }

    private IReadOnlyList<OpportunityFeedItem> ParseZipAtom(
        ReadOnlyMemory<byte> content,
        Uri sourceUri)
    {
        using MemoryStream memory = new(content.ToArray(), writable: false);
        return ParseZipArchive(memory, sourceUri, int.MaxValue);
    }

    public IReadOnlyList<OpportunityFeedItem> ParseZipArchive(
        Stream content,
        Uri sourceUri,
        int maxItems)
    {
        using ZipArchive archive = new(content, ZipArchiveMode.Read, leaveOpen: true);
        List<OpportunityFeedItem> items = [];
        foreach (ZipArchiveEntry entry in archive.Entries
                     .Where(entry => entry.FullName.EndsWith(
                         ".atom",
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => entry.FullName, StringComparer.Ordinal))
        {
            if (entry.Length > MaximumArchiveEntryBytes)
            {
                throw new InvalidDataException(
                    $"La entrada '{entry.FullName}' supera el límite de " +
                    $"{MaximumArchiveEntryBytes} bytes descomprimidos.");
            }

            using Stream stream = entry.Open();
            using StreamReader reader = new(stream);
            int remaining = maxItems - items.Count;
            items.AddRange(ParseAtom(reader.ReadToEnd(), sourceUri).Take(remaining));
            if (items.Count >= maxItems)
            {
                break;
            }
        }

        return items;
    }

    private IReadOnlyList<OpportunityFeedItem> ParseHtml(
        string html,
        Uri sourceUri,
        IReadOnlyList<string> itemSelectors)
    {
        IDocument document = _htmlParser.ParseDocument(html);
        string selector = itemSelectors.Count > 0
            ? string.Join(',', itemSelectors)
            : "article, li";
        return document.QuerySelectorAll(selector)
            .Select(element =>
            {
                string title = element.QuerySelector(
                    "h2, h3, .heading, .title-text, strong")?.TextContent ??
                               element.TextContent;
                IElement? linkElement = element.QuerySelector("a[href]");
                string link = linkElement?.GetAttribute("href") ?? string.Empty;
                string resolved = ResolveUrl(sourceUri, link);
                return new OpportunityFeedItem
                {
                    ExternalId = resolved,
                    Title = TextNormalizer.CleanEvidence(title, 500),
                    Summary = TextNormalizer.CleanEvidence(element.TextContent, 4_000),
                    OfficialUrl = resolved
                };
            })
            .Where(item => item.OfficialUrl.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<OpportunityFeedItem> ParseBocmSummary(
        string xml,
        DateOnly documentDate)
    {
        XDocument document = ParseXml(xml);
        return document.Descendants()
            .Where(element => element.Name.LocalName == "disposicion")
            .Select(disposition =>
            {
                string identifier = ChildValue(disposition, "identificador");
                string title = ChildValue(disposition, "titulo");
                string range = ChildValue(disposition, "rango");
                string officialUrl = ChildValue(disposition, "url_html");
                string authority = disposition.Ancestors()
                    .FirstOrDefault(element => element.Name.LocalName == "organismo")
                    ?.Attribute("nombre")
                    ?.Value
                    ?.Trim() ?? string.Empty;
                string section = disposition.Ancestors()
                    .FirstOrDefault(element => element.Name.LocalName == "seccion")
                    ?.Attribute("nombre")
                    ?.Value
                    ?.Trim() ?? string.Empty;
                return new OpportunityFeedItem
                {
                    ExternalId = identifier,
                    Title = TextNormalizer.CleanEvidence(title, 500),
                    Summary = TextNormalizer.CleanEvidence(
                        $"{authority} {section} {range} {title}",
                        4_000),
                    OfficialUrl = officialUrl,
                    PublishedAtUtc = new DateTimeOffset(
                        documentDate.ToDateTime(TimeOnly.MinValue),
                        TimeSpan.Zero)
                };
            })
            .ToArray();
    }

    private IReadOnlyList<OpportunityFeedItem> ParseEAdminHtml(
        string html,
        Uri sourceUri)
    {
        IDocument document = _htmlParser.ParseDocument(html);
        return document.QuerySelectorAll("tr")
            .Select(row =>
            {
                IElement? linkElement = row.QuerySelector(
                    "a[href*='Tablon.do?action=verAnuncio&id=']");
                IElement[] cells = row.QuerySelectorAll("td").ToArray();
                string link = linkElement?.GetAttribute("href") ?? string.Empty;
                string resolved = ResolveUrl(sourceUri, link);
                string title = cells.Length > 1
                    ? cells[1].TextContent
                    : row.TextContent;
                return new OpportunityFeedItem
                {
                    ExternalId = resolved,
                    Title = TextNormalizer.CleanEvidence(title, 500),
                    Summary = TextNormalizer.CleanEvidence(row.TextContent, 4_000),
                    OfficialUrl = resolved,
                    PublishedAtUtc = ParseEAdminDate(row.TextContent)
                };
            })
            .Where(item => item.OfficialUrl.Length > 0)
            .ToArray();
    }

    private static IReadOnlyList<OpportunityFeedItem> ParseSitemap(
        string xml,
        Uri sourceUri)
    {
        XDocument document = ParseXml(xml);
        if (document.Root?.Name.LocalName != "urlset")
        {
            throw new InvalidDataException(
                $"El sitemap comercial '{sourceUri}' no contiene un urlset.");
        }

        return document.Descendants()
            .Where(element => element.Name.LocalName == "url")
            .Select(item =>
            {
                string location = ChildValue(item, "loc");
                string resolved = ResolveUrl(sourceUri, location);
                string pathText = Uri.TryCreate(resolved, UriKind.Absolute, out Uri? uri)
                    ? Uri.UnescapeDataString(uri.AbsolutePath)
                        .Replace('/', ' ')
                        .Replace('-', ' ')
                        .Replace('_', ' ')
                    : string.Empty;
                string embeddedTitles = string.Join(
                    ' ',
                    item.Descendants()
                        .Where(element => element.Name.LocalName == "title")
                        .Select(element => element.Value)
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                string searchable = TextNormalizer.CleanEvidence(
                    $"{embeddedTitles} {pathText}",
                    2_000);
                return new OpportunityFeedItem
                {
                    ExternalId = resolved,
                    Title = searchable,
                    Summary = searchable,
                    OfficialUrl = resolved,
                    PublishedAtUtc = ParseDate(ChildValue(item, "lastmod"))
                };
            })
            .Where(item => item.OfficialUrl.Length > 0)
            .ToArray();
    }

    private static DateTimeOffset? ParseEAdminDate(string value)
    {
        System.Text.RegularExpressions.Match match =
            System.Text.RegularExpressions.Regex.Match(
                value,
                @"\b(?<date>\d{2}/\d{2}/\d{4})\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success &&
               DateTime.TryParseExact(
                   match.Groups["date"].Value,
                   "dd/MM/yyyy",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out DateTime date)
            ? new DateTimeOffset(
                DateTime.SpecifyKind(date, DateTimeKind.Utc),
                TimeSpan.Zero)
            : null;
    }

    private static XDocument ParseXml(string xml)
    {
        try
        {
            return XDocument.Parse(xml, LoadOptions.None);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException)
        {
            throw new InvalidDataException("El feed XML del radar no es válido.", exception);
        }
    }

    private static string ChildValue(XElement parent, string localName)
    {
        return parent.Elements()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value
            ?.Trim() ?? string.Empty;
    }

    private static string ReadJsonString(JsonElement element, string name)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ResolveUrl(Uri sourceUri, string value)
    {
        return Uri.TryCreate(sourceUri, value, out Uri? resolved) &&
               (resolved.Scheme == Uri.UriSchemeHttp ||
                resolved.Scheme == Uri.UriSchemeHttps)
            ? resolved.AbsoluteUri
            : string.Empty;
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string StripHtml(string value)
    {
        return TextNormalizer.CleanEvidence(
            new HtmlParser().ParseDocument(value).Body?.TextContent,
            4_000);
    }

    private static string Decode(ReadOnlyMemory<byte> content)
    {
        return System.Text.Encoding.UTF8.GetString(content.Span);
    }
}
