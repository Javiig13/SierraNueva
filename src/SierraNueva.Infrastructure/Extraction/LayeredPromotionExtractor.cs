using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Infrastructure.Extraction;

public sealed partial class LayeredPromotionExtractor : IPromotionExtractor
{
    private readonly HtmlParser _parser = new();

    public async Task<IReadOnlyList<Promotion>> ExtractAsync(
        FetchedPage page,
        SourceDefinition source,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (page.ExtractorHint == "pdf")
        {
            Promotion? pdfPromotion = ExtractFromText(
                page.Content,
                page.Url,
                source,
                municipalities,
                "PdfBrochureExtractor");
            return pdfPromotion is null ? [] : [pdfPromotion];
        }

        IDocument document = await _parser.ParseDocumentAsync(page.Content, cancellationToken);
        IElement? contentRoot = string.IsNullOrWhiteSpace(source.ContentSelector)
            ? document.Body
            : document.QuerySelector(source.ContentSelector);
        if (contentRoot is null)
        {
            throw new InvalidDataException(
                $"El selector de contenido '{source.ContentSelector}' no existe en '{page.Url}'.");
        }

        List<IElement> contentRoots = [contentRoot];
        foreach (string selector in source.AdditionalContentSelectors)
        {
            IElement? additionalRoot = document.QuerySelector(selector);
            if (additionalRoot is null)
            {
                throw new InvalidDataException(
                    $"El selector de contenido adicional '{selector}' no existe en '{page.Url}'.");
            }

            contentRoots.Add(additionalRoot);
        }

        string pageText = TextNormalizer.CleanEvidence(
            string.Join(' ', contentRoots.Distinct().Select(root => root.TextContent)),
            100_000);
        Uri canonicalUri = GetCanonicalUri(document, page.Url);
        List<Promotion> promotions = ExtractJsonLd(
            document,
            canonicalUri,
            source,
            municipalities,
            page.FetchedAtUtc);

        if (promotions.Count == 0)
        {
            Promotion? htmlPromotion = ExtractFromHtml(
                document,
                pageText,
                canonicalUri,
                source,
                municipalities,
                page.FetchedAtUtc);
            if (htmlPromotion is not null)
            {
                promotions.Add(htmlPromotion);
            }
        }

        foreach (Promotion promotion in promotions)
        {
            EnrichFromText(promotion, pageText, canonicalUri, page.FetchedAtUtc, "TextPatternExtractor");
            ApplyCustomSelectors(
                promotion,
                document,
                source,
                municipalities,
                canonicalUri,
                page.FetchedAtUtc);
            ApplyFixedMunicipality(
                promotion,
                source,
                canonicalUri,
                page.FetchedAtUtc);
            promotion.BrochureUrls = document.QuerySelectorAll("a[href]")
                .Select(link => link.GetAttribute("href"))
                .Where(href => !string.IsNullOrWhiteSpace(href))
                .Select(href => Uri.TryCreate(canonicalUri, href, out Uri? uri) ? uri : null)
                .Where(uri => uri is not null &&
                              uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
                              (source.AllowedHosts.Count == 0 ||
                               source.AllowedHosts.Contains(
                                   uri.IdnHost,
                                   StringComparer.OrdinalIgnoreCase)))
                .Cast<Uri>()
                .Select(uri => uri.AbsoluteUri)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return promotions
            .DistinctBy(
                promotion => string.Join(
                    '\u001f',
                    promotion.CanonicalUrl,
                    promotion.Name,
                    promotion.PriceFrom?.ToString(CultureInfo.InvariantCulture),
                    promotion.PriceTo?.ToString(CultureInfo.InvariantCulture)),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ApplyFixedMunicipality(
        Promotion promotion,
        SourceDefinition source,
        Uri sourceUrl,
        DateTimeOffset capturedAt)
    {
        if (string.IsNullOrWhiteSpace(source.FixedMunicipality))
        {
            return;
        }

        promotion.Municipality = source.FixedMunicipality;
        AddEvidence(
            promotion,
            "municipality",
            source.FixedMunicipality,
            sourceUrl,
            capturedAt,
            "ReviewedSourceConfiguration",
            0.98m,
            FieldQuality.Explicit,
            $"Municipio fijo revisado para la URL de inicio: {source.FixedMunicipality}");
    }

    private static List<Promotion> ExtractJsonLd(
        IDocument document,
        Uri canonicalUri,
        SourceDefinition source,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        DateTimeOffset capturedAt)
    {
        List<Promotion> promotions = [];
        foreach (IElement script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            string json = script.TextContent.Trim();
            if (json.Length == 0)
            {
                continue;
            }

            try
            {
                using JsonDocument jsonDocument = JsonDocument.Parse(json);
                foreach (JsonElement node in EnumerateNodes(jsonDocument.RootElement))
                {
                    if (!IsPromotionNode(node))
                    {
                        continue;
                    }

                    string? name = ReadString(node, "name", "headline");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    string sourceUrl = ReadString(node, "url") ?? canonicalUri.AbsoluteUri;
                    Promotion promotion = new()
                    {
                        Name = name,
                        CanonicalUrl = Uri.TryCreate(canonicalUri, sourceUrl, out Uri? resolved)
                            ? resolved.AbsoluteUri
                            : canonicalUri.AbsoluteUri,
                        SourceUrls = [canonicalUri.AbsoluteUri],
                        SourceKind = source.SourceKind,
                        DeveloperName = ReadNestedString(node, "brand", "name") ??
                                        ReadNestedString(node, "seller", "name") ??
                                        ReadNestedString(node, "provider", "name"),
                        Address = ReadAddress(node),
                        Locality = ReadNestedString(node, "address", "addressLocality"),
                        PostalCode = ReadNestedString(node, "address", "postalCode"),
                        Latitude = ReadNestedDouble(node, "geo", "latitude"),
                        Longitude = ReadNestedDouble(node, "geo", "longitude"),
                        LocationPrecision = ReadNestedDouble(node, "geo", "latitude").HasValue
                            ? LocationPrecision.ExactCoordinates
                            : LocationPrecision.Unknown,
                        BedroomsMin = ReadInt(node, "numberOfRooms"),
                        CommercialStatus = CommercialStatus.Unknown,
                        ConstructionStatus = ConstructionStatus.Unknown
                    };

                    ApplyOffer(node, promotion);
                    MunicipalityCatalog catalog = new(municipalities);
                    promotion.Municipality =
                        catalog.ResolveOfficialName(ReadAddressLocality(node)) ??
                        catalog.ResolveOfficialName(promotion.Address) ??
                        source.FixedMunicipality ??
                        (source.MunicipalityHints.Count > 0 ? source.MunicipalityHints[0] : null) ??
                        string.Empty;
                    AddEvidence(
                        promotion,
                        "name",
                        promotion.Name,
                        canonicalUri,
                        capturedAt,
                        "JsonLdPromotionExtractor",
                        0.96m,
                        FieldQuality.Explicit,
                        json);
                    if (promotion.PriceFrom.HasValue)
                    {
                        AddEvidence(
                            promotion,
                            "priceFrom",
                            promotion.PriceFrom.Value.ToString(CultureInfo.InvariantCulture),
                            canonicalUri,
                            capturedAt,
                            "JsonLdPromotionExtractor",
                            0.94m,
                            FieldQuality.Explicit,
                            json);
                    }

                    promotions.Add(promotion);
                }
            }
            catch (JsonException)
            {
                // Invalid JSON-LD does not prevent lower extraction layers from running.
            }
        }

        return promotions;
    }

    private static Promotion? ExtractFromHtml(
        IDocument document,
        string text,
        Uri canonicalUri,
        SourceDefinition source,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        DateTimeOffset capturedAt)
    {
        MunicipalityCatalog catalog = new(municipalities);
        string? municipality = catalog.ResolveOfficialName(text) ??
                               source.FixedMunicipality ??
                               (source.MunicipalityHints.Count > 0
                                   ? source.MunicipalityHints[0]
                                   : null);
        bool relevant = PromotionTermsRegex().IsMatch(text) ||
                        source.CustomSelectors.Count > 0;
        if (!relevant || string.IsNullOrWhiteSpace(municipality))
        {
            return null;
        }

        string? name = GetMetaContent(document, "property", "og:title") ??
                       document.QuerySelector("h1")?.TextContent.Trim() ??
                       document.Title;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Promotion promotion = new()
        {
            Name = TextNormalizer.CleanEvidence(name, 160),
            Municipality = municipality,
            CanonicalUrl = canonicalUri.AbsoluteUri,
            SourceUrls = [canonicalUri.AbsoluteUri],
            SourceKind = source.SourceKind
        };
        AddEvidence(
            promotion,
            "name",
            promotion.Name,
            canonicalUri,
            capturedAt,
            "HtmlMetadataExtractor",
            0.82m,
            FieldQuality.Explicit,
            name);
        return promotion;
    }

    private static Promotion? ExtractFromText(
        string text,
        Uri url,
        SourceDefinition source,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        string extractor)
    {
        MunicipalityCatalog catalog = new(municipalities);
        string? municipality = catalog.ResolveOfficialName(text) ??
                               source.FixedMunicipality ??
                               (source.MunicipalityHints.Count > 0
                                   ? source.MunicipalityHints[0]
                                   : null);
        Match nameMatch = PdfTitleRegex().Match(text);
        if (municipality is null || !nameMatch.Success)
        {
            return null;
        }

        Promotion promotion = new()
        {
            Name = TextNormalizer.CleanEvidence(nameMatch.Groups["name"].Value, 160),
            Municipality = municipality,
            CanonicalUrl = url.AbsoluteUri,
            SourceUrls = [url.AbsoluteUri],
            BrochureUrls = [url.AbsoluteUri],
            SourceKind = source.SourceKind
        };
        EnrichFromText(promotion, text, url, DateTimeOffset.UtcNow, extractor);
        return promotion;
    }

    private static void EnrichFromText(
        Promotion promotion,
        string text,
        Uri sourceUrl,
        DateTimeOffset capturedAt,
        string extractor)
    {
        string normalized = TextNormalizer.CleanEvidence(text, 100_000);
        List<decimal> prices = PriceRegex().Matches(normalized)
            .Select(match => (Match)match)
            .Where(match => !IsMonthlyPrice(normalized, match))
            .Select(match => ParseSpanishDecimal(match.Groups["value"].Value))
            .Where(value => value is >= 50_000m and <= 10_000_000m)
            .Cast<decimal>()
            .Distinct()
            .Order()
            .ToList();
        if (prices.Count > 0)
        {
            promotion.PriceFrom ??= prices[0];
            promotion.PriceTo ??= prices.Count > 1 ? prices[^1] : null;
            AddEvidence(
                promotion,
                "priceFrom",
                promotion.PriceFrom.Value.ToString(CultureInfo.InvariantCulture),
                sourceUrl,
                capturedAt,
                extractor,
                0.84m,
                FieldQuality.Normalized,
                FindFragment(normalized, PriceRegex().Match(normalized)));
        }

        ApplyDecimalRange(
            BuiltAreaRegex(),
            normalized,
            10m,
            5_000m,
            (minimum, maximum) =>
            {
                promotion.BuiltAreaMinSqm ??= minimum;
                promotion.BuiltAreaMaxSqm ??= maximum;
            });
        ApplyDecimalRange(
            PlotAreaRegex(),
            normalized,
            20m,
            10_000m,
            (minimum, maximum) =>
            {
                promotion.PlotAreaMinSqm ??= minimum;
                promotion.PlotAreaMaxSqm ??= maximum;
            });
        ApplyIntRange(
            BedroomsRegex(),
            normalized,
            (minimum, maximum) =>
            {
                promotion.BedroomsMin ??= minimum;
                promotion.BedroomsMax ??= maximum;
            });
        ApplyIntRange(
            BathroomsRegex(),
            normalized,
            (minimum, maximum) =>
            {
                promotion.BathroomsMin ??= minimum;
                promotion.BathroomsMax ??= maximum;
            });

        HashSet<string> propertyTypes = new(StringComparer.OrdinalIgnoreCase);
        if (IndependentRegex().IsMatch(normalized))
        {
            propertyTypes.Add("Independiente");
        }

        if (SemiDetachedRegex().IsMatch(normalized))
        {
            propertyTypes.Add("Pareado");
        }

        if (TownhouseRegex().IsMatch(normalized))
        {
            propertyTypes.Add("Adosado");
        }

        promotion.PropertyTypes = promotion.PropertyTypes.Concat(propertyTypes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        promotion.HasPrivatePool ??= PrivatePoolRegex().IsMatch(normalized) ? true : null;
        promotion.HasCommunityPool ??= CommunityPoolRegex().IsMatch(normalized) ? true : null;
        promotion.CommercialStatus = DetectCommercialStatus(normalized, promotion.CommercialStatus);
        promotion.ConstructionStatus = DetectConstructionStatus(normalized, promotion.ConstructionStatus);
        if (promotion.ConstructionStatus == ConstructionStatus.Licensed)
        {
            promotion.BuildingLicenceStatus ??= "Concedida";
        }
        else if (promotion.ConstructionStatus == ConstructionStatus.Planned &&
                 LicenceRequestedRegex().IsMatch(normalized))
        {
            promotion.BuildingLicenceStatus ??= "Solicitada";
        }

        Match delivery = DeliveryRegex().Match(normalized);
        if (delivery.Success && promotion.DeliveryDateText is null)
        {
            promotion.DeliveryDateText = TextNormalizer.CleanEvidence(delivery.Value, 100);
            promotion.EstimatedDeliveryDate = EstimateDelivery(delivery);
        }

        Match units = UnitsRegex().Match(normalized);
        if (units.Success && int.TryParse(units.Groups["value"].Value, out int totalUnits))
        {
            promotion.TotalUnits ??= totalUnits;
        }

        Match available = AvailableUnitsRegex().Match(normalized);
        if (available.Success && int.TryParse(available.Groups["value"].Value, out int availableUnits))
        {
            promotion.AvailableUnits ??= availableUnits;
        }
        else if (SingleAvailableUnitRegex().IsMatch(normalized))
        {
            promotion.AvailableUnits ??= 1;
        }

        if (promotion.AvailableUnits == 1 &&
            promotion.CommercialStatus == CommercialStatus.Unknown)
        {
            promotion.CommercialStatus = CommercialStatus.LastUnits;
        }

        Match developer = DeveloperRegex().Match(normalized);
        if (developer.Success)
        {
            promotion.DeveloperName ??= TextNormalizer.CleanEvidence(
                developer.Groups["value"].Value,
                120);
        }

        AddPatternEvidence(
            promotion,
            "propertyTypes",
            string.Join(", ", promotion.PropertyTypes),
            PromotionTermsRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "bedrooms",
            FormatRange(promotion.BedroomsMin, promotion.BedroomsMax),
            BedroomsRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "bathrooms",
            FormatRange(promotion.BathroomsMin, promotion.BathroomsMax),
            BathroomsRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "builtAreaSqm",
            FormatRange(promotion.BuiltAreaMinSqm, promotion.BuiltAreaMaxSqm),
            BuiltAreaRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "plotAreaSqm",
            FormatRange(promotion.PlotAreaMinSqm, promotion.PlotAreaMaxSqm),
            PlotAreaRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "commercialStatus",
            promotion.CommercialStatus == CommercialStatus.Unknown
                ? string.Empty
                : promotion.CommercialStatus.ToString(),
            CommercialStatusRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "constructionStatus",
            promotion.ConstructionStatus == ConstructionStatus.Unknown
                ? string.Empty
                : promotion.ConstructionStatus.ToString(),
            ConstructionStatusRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "deliveryDateText",
            promotion.DeliveryDateText ?? string.Empty,
            DeliveryRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
        AddPatternEvidence(
            promotion,
            "developerName",
            promotion.DeveloperName ?? string.Empty,
            DeveloperRegex(),
            normalized,
            sourceUrl,
            capturedAt,
            extractor);
    }

    private static void ApplyCustomSelectors(
        Promotion promotion,
        IDocument document,
        SourceDefinition source,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        Uri sourceUrl,
        DateTimeOffset capturedAt)
    {
        foreach ((string field, string selector) in source.CustomSelectors)
        {
            string? value = document.QuerySelector(selector)?.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (field.ToLowerInvariant())
            {
                case "name":
                    promotion.Name = TextNormalizer.CleanEvidence(value, 160);
                    break;
                case "developername":
                    promotion.DeveloperName = TextNormalizer.CleanEvidence(value, 120);
                    break;
                case "municipality":
                    promotion.Municipality =
                        new MunicipalityCatalog(municipalities).ResolveOfficialName(value) ??
                        TextNormalizer.CleanEvidence(value, 100);
                    break;
                case "address":
                    promotion.Address = TextNormalizer.CleanEvidence(value, 180);
                    break;
                case "pricefrom":
                    promotion.PriceFrom = ParseSpanishDecimal(value);
                    break;
                case "totalunits":
                    if (FirstIntegerRegex().Match(value) is { Success: true } totalMatch &&
                        int.TryParse(totalMatch.Value, out int totalUnits))
                    {
                        promotion.TotalUnits = totalUnits;
                    }

                    break;
                case "propertytypes":
                    promotion.PropertyTypes = ExtractPropertyTypes(value);
                    break;
            }

            AddEvidence(
                promotion,
                field,
                value,
                sourceUrl,
                capturedAt,
                "DomainSpecificSelectorExtractor",
                0.92m,
                FieldQuality.Explicit,
                value);
        }
    }

    private static IReadOnlyList<string> ExtractPropertyTypes(string value)
    {
        List<string> propertyTypes = [];
        if (IndependentRegex().IsMatch(value))
        {
            propertyTypes.Add("Independiente");
        }

        if (SemiDetachedRegex().IsMatch(value))
        {
            propertyTypes.Add("Pareado");
        }

        if (TownhouseRegex().IsMatch(value))
        {
            propertyTypes.Add("Adosado");
        }

        return propertyTypes;
    }

    private static IEnumerable<JsonElement> EnumerateNodes(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (JsonElement nested in EnumerateNodes(item))
                {
                    yield return nested;
                }
            }

            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return element;
        if (TryGetProperty(element, "@graph", out JsonElement graph))
        {
            foreach (JsonElement nested in EnumerateNodes(graph))
            {
                yield return nested;
            }
        }
    }

    private static bool IsPromotionNode(JsonElement node)
    {
        string type = ReadString(node, "@type") ?? string.Empty;
        string normalized = type.ToLowerInvariant();
        bool supportedType = normalized.Contains("product", StringComparison.Ordinal) ||
                             normalized.Contains("residence", StringComparison.Ordinal) ||
                             normalized.Contains("house", StringComparison.Ordinal) ||
                             normalized.Contains("apartment", StringComparison.Ordinal) ||
                             normalized.Contains("realestatelisting", StringComparison.Ordinal) ||
                             normalized.Contains("accommodation", StringComparison.Ordinal);
        return supportedType &&
               (!string.IsNullOrWhiteSpace(ReadString(node, "name")) ||
                TryGetProperty(node, "offers", out _));
    }

    private static void ApplyOffer(JsonElement node, Promotion promotion)
    {
        if (!TryGetProperty(node, "offers", out JsonElement offers))
        {
            return;
        }

        IEnumerable<JsonElement> candidates = offers.ValueKind == JsonValueKind.Array
            ? offers.EnumerateArray()
            : [offers];
        List<decimal> values = [];
        foreach (JsonElement offer in candidates)
        {
            foreach (string property in new[] { "lowPrice", "price", "highPrice" })
            {
                decimal? value = ReadDecimal(offer, property);
                if (value.HasValue)
                {
                    values.Add(value.Value);
                }
            }
        }

        if (values.Count > 0)
        {
            promotion.PriceFrom = values.Min();
            promotion.PriceTo = values.Count > 1 ? values.Max() : null;
        }
    }

    private static string? ReadAddress(JsonElement node)
    {
        if (!TryGetProperty(node, "address", out JsonElement address))
        {
            return null;
        }

        if (address.ValueKind == JsonValueKind.String)
        {
            return address.GetString();
        }

        return string.Join(
            ", ",
            new[]
            {
                ReadString(address, "streetAddress"),
                ReadString(address, "addressLocality"),
                ReadString(address, "postalCode")
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? ReadAddressLocality(JsonElement node)
    {
        return TryGetProperty(node, "address", out JsonElement address)
            ? ReadString(address, "addressLocality")
            : null;
    }

    private static string? ReadNestedString(JsonElement node, string parent, string child)
    {
        return TryGetProperty(node, parent, out JsonElement nested)
            ? ReadString(nested, child)
            : null;
    }

    private static double? ReadNestedDouble(JsonElement node, string parent, string child)
    {
        return TryGetProperty(node, parent, out JsonElement nested)
            ? ReadDouble(nested, child)
            : null;
    }

    private static string? ReadString(JsonElement node, params string[] names)
    {
        foreach (string name in names)
        {
            if (!TryGetProperty(node, name, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetRawText();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().FirstOrDefault().ToString();
            }
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement node, string name)
    {
        if (!TryGetProperty(node, name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number)
            ? number
            : ParseSpanishDecimal(value.ToString());
    }

    private static double? ReadDouble(JsonElement node, string name)
    {
        if (!TryGetProperty(node, name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)
            ? number
            : double.TryParse(
                value.ToString().Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out number)
                ? number
                : null;
    }

    private static int? ReadInt(JsonElement node, string name)
    {
        if (!TryGetProperty(node, name, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : int.TryParse(value.ToString(), out number)
                ? number
                : null;
    }

    private static bool TryGetProperty(JsonElement node, string name, out JsonElement value)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static Uri GetCanonicalUri(IDocument document, Uri fallback)
    {
        string? canonical = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href") ??
                            GetMetaContent(document, "property", "og:url");
        return Uri.TryCreate(fallback, canonical, out Uri? uri) ? uri : fallback;
    }

    private static string? GetMetaContent(
        IDocument document,
        string attribute,
        string value)
    {
        return document.QuerySelector($"meta[{attribute}='{value}']")?.GetAttribute("content");
    }

    private static decimal? ParseSpanishDecimal(string value)
    {
        string normalized = value
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("euros", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("euro", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        int comma = normalized.LastIndexOf(',');
        int dot = normalized.LastIndexOf('.');
        if (comma >= 0 && comma > dot)
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal)
                .Replace(',', '.');
        }
        else if (dot >= 0 && normalized.Length - dot - 1 == 3)
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : null;
    }

    private static bool IsMonthlyPrice(string text, Match match)
    {
        int start = Math.Max(0, match.Index - 30);
        int length = Math.Min(text.Length - start, match.Length + 70);
        string context = text.Substring(start, length);
        return MonthlyRegex().IsMatch(context);
    }

    private static void ApplyDecimalRange(
        Regex regex,
        string text,
        decimal minimumAllowed,
        decimal maximumAllowed,
        Action<decimal, decimal?> setter)
    {
        Match match = regex.Match(text);
        if (!match.Success)
        {
            return;
        }

        decimal? minimum = ParseSpanishDecimal(match.Groups["min"].Value);
        decimal? maximum = ParseSpanishDecimal(match.Groups["max"].Value);
        if (minimum is >= 0 &&
            minimum >= minimumAllowed &&
            minimum <= maximumAllowed &&
            (!maximum.HasValue ||
             (maximum >= minimumAllowed && maximum <= maximumAllowed)))
        {
            setter(minimum.Value, maximum);
        }
    }

    private static void ApplyIntRange(
        Regex regex,
        string text,
        Action<int, int?> setter)
    {
        Match match = regex.Match(text);
        if (!match.Success || !int.TryParse(match.Groups["min"].Value, out int minimum))
        {
            return;
        }

        int? maximum = int.TryParse(match.Groups["max"].Value, out int parsedMax)
            ? parsedMax
            : null;
        setter(minimum, maximum);
    }

    private static CommercialStatus DetectCommercialStatus(
        string text,
        CommercialStatus fallback)
    {
        if (SoldOutRegex().IsMatch(text))
        {
            return CommercialStatus.SoldOut;
        }

        if (LastUnitsRegex().IsMatch(text))
        {
            return CommercialStatus.LastUnits;
        }

        if (UpcomingRegex().IsMatch(text))
        {
            return CommercialStatus.Upcoming;
        }

        if (PreSalesRegex().IsMatch(text))
        {
            return CommercialStatus.PreSales;
        }

        return OnSaleRegex().IsMatch(text) ? CommercialStatus.OnSale : fallback;
    }

    private static ConstructionStatus DetectConstructionStatus(
        string text,
        ConstructionStatus fallback)
    {
        if (CompletedRegex().IsMatch(text))
        {
            return ConstructionStatus.Completed;
        }

        if (UnderConstructionRegex().IsMatch(text))
        {
            return ConstructionStatus.UnderConstruction;
        }

        if (LicensedRegex().IsMatch(text))
        {
            return ConstructionStatus.Licensed;
        }

        return LicenceRequestedRegex().IsMatch(text) ? ConstructionStatus.Planned : fallback;
    }

    private static DateOnly? EstimateDelivery(Match match)
    {
        if (!int.TryParse(match.Groups["year"].Value, out int year))
        {
            return null;
        }

        int quarter = int.TryParse(match.Groups["quarter"].Value, out int parsedQuarter)
            ? Math.Clamp(parsedQuarter, 1, 4)
            : 4;
        int month = quarter * 3;
        return new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    }

    private static string FindFragment(string text, Match match)
    {
        if (!match.Success)
        {
            return string.Empty;
        }

        int start = Math.Max(0, match.Index - 60);
        int length = Math.Min(text.Length - start, match.Length + 120);
        return TextNormalizer.CleanEvidence(text.Substring(start, length));
    }

    private static string FormatRange<T>(T? minimum, T? maximum)
        where T : struct
    {
        return (minimum, maximum) switch
        {
            (null, null) => string.Empty,
            ({ } min, { } max) => $"{min}-{max}",
            ({ } min, _) => min.ToString() ?? string.Empty,
            _ => maximum?.ToString() ?? string.Empty
        };
    }

    private static void AddPatternEvidence(
        Promotion promotion,
        string field,
        string value,
        Regex regex,
        string text,
        Uri sourceUrl,
        DateTimeOffset capturedAt,
        string extractor)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            promotion.Evidence.Any(item => item.Field == field))
        {
            return;
        }

        Match match = regex.Match(text);
        AddEvidence(
            promotion,
            field,
            value,
            sourceUrl,
            capturedAt,
            extractor,
            0.78m,
            FieldQuality.Normalized,
            FindFragment(text, match));
    }

    private static void AddEvidence(
        Promotion promotion,
        string field,
        string value,
        Uri sourceUrl,
        DateTimeOffset capturedAt,
        string extractor,
        decimal confidence,
        FieldQuality quality,
        string fragment)
    {
        promotion.Evidence = promotion.Evidence.Append(new EvidenceItem
        {
            Field = field,
            ValueText = TextNormalizer.CleanEvidence(value, 160),
            SourceUrl = sourceUrl.AbsoluteUri,
            CapturedAtUtc = capturedAt,
            Extractor = extractor,
            Confidence = confidence,
            Quality = quality,
            TextFragment = TextNormalizer.CleanEvidence(fragment)
        }).ToArray();
    }

    [GeneratedRegex(
        @"\b(?:obra\s+nueva|promoci[oó]n|residencial|viviendas?|chalets?|adosados?|pareados?|unifamiliares?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromotionTermsRegex();

    [GeneratedRegex(
        @"(?:desde|precio(?:\s+desde)?|a\s+partir\s+de)?\s*(?<value>\d{2,3}(?:[.\s]\d{3})+|\d{5,7})(?:,\d{1,2})?\s*(?:€|EUR|euros?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(
        @"(?:(?:superficie\s+)?construid[ao]s?\D{0,35}(?<min>\d{2,4}(?:[.,]\d+)?)(?:\s*m(?:²|2))?(?:\s*(?:hasta|a|y|-)\s*(?<max>\d{2,4}(?:[.,]\d+)?))?\s*m(?:²|2)|(?<min>\d{2,4}(?:[.,]\d+)?)\s*(?:hasta|a|y|-)\s*(?<max>\d{2,4}(?:[.,]\d+)?)\s*m(?:²|2)(?:\s+\p{L}+){0,2}\s+construid[ao]s?|(?<min>\d{2,4}(?:[.,]\d+)?)\s*m(?:²|2)(?:\s+\p{L}+){0,2}\s+construid[ao]s?|(?<min>\d{2,4}(?:[.,]\d+)?)\s*m(?:²|2)(?=\s+\d{1,2}\s+(?:habitaciones?|dormitorios?))|(?:metros?\s*(?:²|2)|superficie)\s*:?\s*(?:desde\s+)?(?<min>\d{2,4}(?:[.,]\d+)?)(?:\s*m(?:²|2))?\s*(?:hasta|a|y|-)\s*(?<max>\d{2,4}(?:[.,]\d+)?)\s*m(?:²|2)(?:\s+\p{L}+){0,2}\s+construid[ao]s?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuiltAreaRegex();

    [GeneratedRegex(
        @"parcelas?\D{0,35}(?<min>\d{2,5}(?:[.,]\d+)?)(?:\s*m(?:²|2))?(?:\s*(?:hasta|a|y|-)\s*(?<max>\d{2,5}(?:[.,]\d+)?))?\s*m(?:²|2)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlotAreaRegex();

    [GeneratedRegex(
        @"(?<min>\d{1,2})(?:\s*(?:a|o|y|-)\s*(?<max>\d{1,2}))?\s+(?:dormitorios?|habitaciones?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BedroomsRegex();

    [GeneratedRegex(
        @"(?<min>\d{1,2})(?:\s*(?:a|-)\s*(?<max>\d{1,2}))?\s+ba[nñ]os?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BathroomsRegex();

    [GeneratedRegex(
        @"\b(?:chalet|vivienda)s?(?:\s+unifamiliares?)?\s+independientes?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex IndependentRegex();

    [GeneratedRegex(@"\bparead[oa]s?\b", RegexOptions.IgnoreCase)]
    private static partial Regex SemiDetachedRegex();

    [GeneratedRegex(@"\badosad[oa]s?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TownhouseRegex();

    [GeneratedRegex(@"\bpiscina\s+privada\b", RegexOptions.IgnoreCase)]
    private static partial Regex PrivatePoolRegex();

    [GeneratedRegex(@"\bpiscina\s+(?:comunitaria|com[uú]n)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CommunityPoolRegex();

    [GeneratedRegex(@"\b(?:agotad[oa]|vendid[oa]\s+al\s+100\s*%)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SoldOutRegex();

    [GeneratedRegex(@"\b[uú]ltimas?\s+(?:viviendas?|unidades?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LastUnitsRegex();

    [GeneratedRegex(@"\b(?:pr[oó]ximamente|pr[oó]xima\s+fase)\b", RegexOptions.IgnoreCase)]
    private static partial Regex UpcomingRegex();

    [GeneratedRegex(@"\b(?:en\s+venta|a\s+la\s+venta|comercializaci[oó]n)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OnSaleRegex();

    [GeneratedRegex(@"precomercializaci[oó]n", RegexOptions.IgnoreCase)]
    private static partial Regex PreSalesRegex();

    [GeneratedRegex(@"\b(?:obra\s+finalizada|llave\s+en\s+mano)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CompletedRegex();

    [GeneratedRegex(
        @"\b(?:en\s+construcci[oó]n|obras?\s+iniciadas?|obra\s+en\s+ejecuci[oó]n)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex UnderConstructionRegex();

    [GeneratedRegex(@"\blicencia\s+(?:concedida|otorgada)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LicensedRegex();

    [GeneratedRegex(@"\blicencia\s+de\s+obra\s+solicitada\b", RegexOptions.IgnoreCase)]
    private static partial Regex LicenceRequestedRegex();

    [GeneratedRegex(
        @"(?<all>(?:entrega|finalizaci[oó]n)(?:\s+prevista)?\D{0,20}(?:(?<quarter>[1-4])(?:er|º|o)?\s*(?:trimestre|T)|Q(?<quarterQ>[1-4]))?\s*(?<year>20\d{2}))",
        RegexOptions.IgnoreCase)]
    private static partial Regex DeliveryRegex();

    [GeneratedRegex(
        @"(?<value>\d{1,4})\s+(?:(?:nuevas?|exclusivas?)\s+)?(?:viviendas?|chalets?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex UnitsRegex();

    [GeneratedRegex(@"(?<value>\d{1,3})\s+(?:viviendas?|unidades?)\s+disponibles?", RegexOptions.IgnoreCase)]
    private static partial Regex AvailableUnitsRegex();

    [GeneratedRegex(
        @"\b(?:[uú]ltima|[uú]nica)\s+(?:(?:vivienda|villa|chalet)\s+(?:disponible|a\s+la\s+venta)|unidad)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SingleAvailableUnitRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex FirstIntegerRegex();

    [GeneratedRegex(
        @"(?:promueve|promotora|desarrollado\s+por)\s*:?\s*(?<value>[\p{L}\d .,&'-]{2,80}?)(?=\s+(?:obras?|entrega|en\s+venta|promoci[oó]n)|$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DeveloperRegex();

    [GeneratedRegex(@"\b(?:al\s+mes|mensual(?:es)?|/\s*mes)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthlyRegex();

    [GeneratedRegex(
        @"(?<name>(?:residencial|promoci[oó]n)\s+[\p{L}\d .&'-]{3,100}?)(?=\s*[:\r\n]|$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PdfTitleRegex();

    [GeneratedRegex(
        @"\b(?:agotad[oa]|[uú]ltimas?\s+unidades?|pr[oó]ximamente|precomercializaci[oó]n|en\s+venta|comercializaci[oó]n)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex CommercialStatusRegex();

    [GeneratedRegex(
        @"\b(?:obra\s+finalizada|llave\s+en\s+mano|en\s+construcci[oó]n|obras?\s+iniciadas?|obra\s+en\s+ejecuci[oó]n|licencia\s+(?:concedida|otorgada)|licencia\s+de\s+obra\s+solicitada)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConstructionStatusRegex();
}
