using System.Globalization;
using System.Text.RegularExpressions;
using SierraNueva.Contracts;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Enrichment;

public static partial class PromotionEnrichmentPolicy
{
    private static readonly HashSet<string> AllowedFields =
        new(StringComparer.Ordinal)
        {
            "address",
            "postalCode",
            "developerName",
            "marketerName",
            "cooperativeName",
            "totalUnits",
            "availableUnits",
            "priceFrom",
            "priceTo",
            "bedroomsMin",
            "bedroomsMax",
            "bathroomsMin",
            "bathroomsMax",
            "usableAreaMinSqm",
            "usableAreaMaxSqm",
            "builtAreaMinSqm",
            "builtAreaMaxSqm",
            "plotAreaMinSqm",
            "plotAreaMaxSqm",
            "garageSpacesMin",
            "garageSpacesMax",
            "deliveryDateText",
            "buildingLicenceStatus"
        };

    public static IReadOnlyList<string> MissingFields(Promotion promotion)
    {
        List<string> fields = [];
        AddIfMissing(fields, "address", promotion.Address);
        AddIfMissing(fields, "postalCode", promotion.PostalCode);
        AddIfMissing(fields, "developerName", promotion.DeveloperName);
        AddIfMissing(fields, "marketerName", promotion.MarketerName);
        AddIfMissing(fields, "cooperativeName", promotion.CooperativeName);
        AddIfMissing(fields, "totalUnits", promotion.TotalUnits);
        AddIfMissing(fields, "availableUnits", promotion.AvailableUnits);
        AddIfMissing(fields, "priceFrom", promotion.PriceFrom);
        AddIfMissing(fields, "priceTo", promotion.PriceTo);
        AddIfMissing(fields, "bedroomsMin", promotion.BedroomsMin);
        AddIfMissing(fields, "bedroomsMax", promotion.BedroomsMax);
        AddIfMissing(fields, "bathroomsMin", promotion.BathroomsMin);
        AddIfMissing(fields, "bathroomsMax", promotion.BathroomsMax);
        AddIfMissing(fields, "usableAreaMinSqm", promotion.UsableAreaMinSqm);
        AddIfMissing(fields, "usableAreaMaxSqm", promotion.UsableAreaMaxSqm);
        AddIfMissing(fields, "builtAreaMinSqm", promotion.BuiltAreaMinSqm);
        AddIfMissing(fields, "builtAreaMaxSqm", promotion.BuiltAreaMaxSqm);
        AddIfMissing(fields, "plotAreaMinSqm", promotion.PlotAreaMinSqm);
        AddIfMissing(fields, "plotAreaMaxSqm", promotion.PlotAreaMaxSqm);
        AddIfMissing(fields, "garageSpacesMin", promotion.GarageSpacesMin);
        AddIfMissing(fields, "garageSpacesMax", promotion.GarageSpacesMax);
        AddIfMissing(fields, "deliveryDateText", promotion.DeliveryDateText);
        AddIfMissing(fields, "buildingLicenceStatus", promotion.BuildingLicenceStatus);
        return fields;
    }

    public static IReadOnlyList<EnrichmentFieldProposal> Validate(
        EnrichmentEvidenceDocument evidence,
        IReadOnlyList<string> missingFields,
        IEnumerable<EnrichmentFieldProposal> proposals,
        decimal minimumConfidence,
        out IReadOnlyList<string> warnings)
    {
        HashSet<string> missing = missingFields.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, EnrichmentEvidencePage> pages = evidence.Pages
            .ToDictionary(page => page.Url, StringComparer.Ordinal);
        List<EnrichmentFieldProposal> valid = [];
        List<string> issues = [];

        foreach (EnrichmentFieldProposal proposal in proposals)
        {
            if (!AllowedFields.Contains(proposal.Field) || !missing.Contains(proposal.Field))
            {
                issues.Add($"Campo descartado: {proposal.Field} no está permitido o ya tenía valor.");
                continue;
            }

            if (proposal.Confidence < minimumConfidence)
            {
                issues.Add($"Campo descartado: {proposal.Field} no alcanza la confianza mínima.");
                continue;
            }

            if (!pages.TryGetValue(proposal.SourceUrl, out EnrichmentEvidencePage? page) ||
                string.IsNullOrWhiteSpace(proposal.EvidenceText) ||
                !Normalize(page.Text).Contains(
                    Normalize(proposal.EvidenceText),
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Campo descartado: {proposal.Field} no tiene una cita verificable.");
                continue;
            }

            if (!CanParse(proposal.Field, proposal.ValueText))
            {
                issues.Add($"Campo descartado: {proposal.Field} tiene un valor no válido.");
                continue;
            }

            if (valid.Any(item => item.Field == proposal.Field))
            {
                issues.Add($"Campo descartado: {proposal.Field} está duplicado.");
                continue;
            }

            valid.Add(new()
            {
                Field = proposal.Field,
                ValueText = proposal.ValueText.Trim(),
                SourceUrl = proposal.SourceUrl,
                EvidenceText = proposal.EvidenceText.Trim(),
                Confidence = Math.Clamp(proposal.Confidence, 0m, 1m)
            });
        }

        warnings = issues;
        return valid.OrderBy(item => item.Field, StringComparer.Ordinal).ToArray();
    }

    public static void ApplyAccepted(
        Promotion promotion,
        PromotionEnrichment enrichment,
        DateTimeOffset now)
    {
        if (enrichment.Status != EnrichmentReviewStatus.Accepted ||
            enrichment.ReviewedAtUtc is null ||
            now - enrichment.EvidenceFetchedAtUtc > TimeSpan.FromDays(30))
        {
            return;
        }

        bool legacyWholeProposal = enrichment.Fields.All(
            field => field.Status == EnrichmentReviewStatus.Pending &&
                     field.ReviewedAtUtc is null);
        foreach (EnrichmentFieldProposal field in enrichment.Fields)
        {
            if (!legacyWholeProposal && field.Status != EnrichmentReviewStatus.Accepted)
            {
                continue;
            }

            Apply(promotion, field, enrichment.EvidenceFetchedAtUtc);
        }
    }

    private static void Apply(
        Promotion promotion,
        EnrichmentFieldProposal field,
        DateTimeOffset capturedAtUtc)
    {
        string value = field.ValueText;
        bool applied = true;
        switch (field.Field)
        {
            case "address" when promotion.Address is null:
                promotion.Address = value;
                break;
            case "postalCode" when promotion.PostalCode is null:
                promotion.PostalCode = value;
                break;
            case "developerName" when promotion.DeveloperName is null:
                promotion.DeveloperName = value;
                break;
            case "marketerName" when promotion.MarketerName is null:
                promotion.MarketerName = value;
                break;
            case "cooperativeName" when promotion.CooperativeName is null:
                promotion.CooperativeName = value;
                break;
            case "deliveryDateText" when promotion.DeliveryDateText is null:
                promotion.DeliveryDateText = value;
                break;
            case "buildingLicenceStatus" when promotion.BuildingLicenceStatus is null:
                promotion.BuildingLicenceStatus = value;
                break;
            case "address" or "postalCode" or "developerName" or "marketerName" or
                "cooperativeName" or "deliveryDateText" or "buildingLicenceStatus":
                applied = false;
                break;
            default:
                applied = ApplyNumber(promotion, field.Field, value);
                break;
        }

        if (!applied)
        {
            return;
        }

        promotion.Evidence = promotion.Evidence.Append(new EvidenceItem
        {
            Field = field.Field,
            ValueText = value,
            SourceUrl = field.SourceUrl,
            CapturedAtUtc = capturedAtUtc,
            Extractor = "reviewed-ai-enrichment",
            Confidence = field.Confidence,
            Quality = FieldQuality.Explicit,
            TextFragment = field.EvidenceText
        }).ToArray();
    }

    private static bool ApplyNumber(Promotion promotion, string field, string value)
    {
        if (field is "totalUnits" or "availableUnits" or "bedroomsMin" or "bedroomsMax" or
            "bathroomsMin" or "bathroomsMax" or "garageSpacesMin" or "garageSpacesMax")
        {
            int parsed = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return field switch
            {
                "totalUnits" when promotion.TotalUnits is null => Set(() => promotion.TotalUnits = parsed),
                "availableUnits" when promotion.AvailableUnits is null => Set(() => promotion.AvailableUnits = parsed),
                "bedroomsMin" when promotion.BedroomsMin is null => Set(() => promotion.BedroomsMin = parsed),
                "bedroomsMax" when promotion.BedroomsMax is null => Set(() => promotion.BedroomsMax = parsed),
                "bathroomsMin" when promotion.BathroomsMin is null => Set(() => promotion.BathroomsMin = parsed),
                "bathroomsMax" when promotion.BathroomsMax is null => Set(() => promotion.BathroomsMax = parsed),
                "garageSpacesMin" when promotion.GarageSpacesMin is null => Set(() => promotion.GarageSpacesMin = parsed),
                "garageSpacesMax" when promotion.GarageSpacesMax is null => Set(() => promotion.GarageSpacesMax = parsed),
                _ => false
            };
        }

        decimal parsedDecimal = decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
        return field switch
        {
            "priceFrom" when promotion.PriceFrom is null => Set(() => promotion.PriceFrom = parsedDecimal),
            "priceTo" when promotion.PriceTo is null => Set(() => promotion.PriceTo = parsedDecimal),
            "usableAreaMinSqm" when promotion.UsableAreaMinSqm is null => Set(() => promotion.UsableAreaMinSqm = parsedDecimal),
            "usableAreaMaxSqm" when promotion.UsableAreaMaxSqm is null => Set(() => promotion.UsableAreaMaxSqm = parsedDecimal),
            "builtAreaMinSqm" when promotion.BuiltAreaMinSqm is null => Set(() => promotion.BuiltAreaMinSqm = parsedDecimal),
            "builtAreaMaxSqm" when promotion.BuiltAreaMaxSqm is null => Set(() => promotion.BuiltAreaMaxSqm = parsedDecimal),
            "plotAreaMinSqm" when promotion.PlotAreaMinSqm is null => Set(() => promotion.PlotAreaMinSqm = parsedDecimal),
            "plotAreaMaxSqm" when promotion.PlotAreaMaxSqm is null => Set(() => promotion.PlotAreaMaxSqm = parsedDecimal),
            _ => false
        };

        static bool Set(Action assignment)
        {
            assignment();
            return true;
        }
    }

    private static bool CanParse(string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            return false;
        }

        if (field is "totalUnits" or "availableUnits" or "bedroomsMin" or "bedroomsMax" or
            "bathroomsMin" or "bathroomsMax" or "garageSpacesMin" or "garageSpacesMax")
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
                   parsed >= 0;
        }

        if (field is "priceFrom" or "priceTo" or "usableAreaMinSqm" or
            "usableAreaMaxSqm" or "builtAreaMinSqm" or "builtAreaMaxSqm" or
            "plotAreaMinSqm" or "plotAreaMaxSqm")
        {
            return decimal.TryParse(
                       value,
                       NumberStyles.Number,
                       CultureInfo.InvariantCulture,
                       out decimal parsed) &&
                   parsed >= 0;
        }

        return true;
    }

    private static string Normalize(string value)
    {
        return Whitespace().Replace(value, " ").Trim();
    }

    private static void AddIfMissing<T>(ICollection<string> fields, string field, T? value)
    {
        if (value is null)
        {
            fields.Add(field);
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
