namespace SierraNueva.Contracts;

public sealed class PromotionDataset
{
    public string SchemaVersion { get; init; } = "1.0";

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public string RunId { get; init; } = string.Empty;

    public int SourceCount { get; init; }

    public int SuccessfulSourceCount { get; init; }

    public int FailedSourceCount { get; init; }

    public IReadOnlyList<Promotion> Promotions { get; init; } = [];

    public PromotionStatistics Statistics { get; init; } = new();
}

public sealed class Promotion
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string Municipality { get; set; } = string.Empty;

    public string? Locality { get; set; }

    public string? Address { get; set; }

    public string? PostalCode { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public LocationPrecision LocationPrecision { get; set; }

    public IReadOnlyList<string> PropertyTypes { get; set; } = [];

    public CommercialStatus CommercialStatus { get; set; }

    public ConstructionStatus ConstructionStatus { get; set; }

    public SourceKind SourceKind { get; set; }

    public decimal SourceConfidence { get; set; }

    public SourceConfidenceExplanation? SourceConfidenceExplanation { get; set; }

    public string? DeveloperName { get; set; }

    public string? MarketerName { get; set; }

    public string? CooperativeName { get; set; }

    public int? TotalUnits { get; set; }

    public int? AvailableUnits { get; set; }

    public decimal? PriceFrom { get; set; }

    public decimal? PriceTo { get; set; }

    public string Currency { get; set; } = "EUR";

    public int? BedroomsMin { get; set; }

    public int? BedroomsMax { get; set; }

    public int? BathroomsMin { get; set; }

    public int? BathroomsMax { get; set; }

    public decimal? UsableAreaMinSqm { get; set; }

    public decimal? UsableAreaMaxSqm { get; set; }

    public decimal? BuiltAreaMinSqm { get; set; }

    public decimal? BuiltAreaMaxSqm { get; set; }

    public decimal? PlotAreaMinSqm { get; set; }

    public decimal? PlotAreaMaxSqm { get; set; }

    public int? GarageSpacesMin { get; set; }

    public int? GarageSpacesMax { get; set; }

    public bool? HasPrivatePool { get; set; }

    public bool? HasCommunityPool { get; set; }

    public string? DeliveryDateText { get; set; }

    public DateOnly? EstimatedDeliveryDate { get; set; }

    public string? BuildingLicenceStatus { get; set; }

    public string CanonicalUrl { get; set; } = string.Empty;

    public IReadOnlyList<string> SourceUrls { get; set; } = [];

    public IReadOnlyList<string> BrochureUrls { get; set; } = [];

    public DateTimeOffset FirstSeenUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; }

    public DateTimeOffset LastChangedUtc { get; set; }

    public int ConsecutiveMisses { get; set; }

    public bool Active { get; set; } = true;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<EvidenceItem> Evidence { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public sealed class SourceConfidenceExplanation
{
    public decimal BaseScore { get; init; }

    public decimal FinalScore { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<SourceConfidenceSignal> Signals { get; init; } = [];
}

public sealed class SourceConfidenceSignal
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public decimal Impact { get; init; }
}

public sealed class EvidenceItem
{
    public string Field { get; init; } = string.Empty;

    public string ValueText { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateTimeOffset CapturedAtUtc { get; init; }

    public string Extractor { get; init; } = string.Empty;

    public decimal Confidence { get; init; }

    public FieldQuality Quality { get; init; }

    public string TextFragment { get; init; } = string.Empty;
}

public sealed class PromotionStatistics
{
    public int Total { get; init; }

    public int Active { get; init; }

    public int WithPrice { get; init; }

    public int WithCoordinates { get; init; }

    public IReadOnlyDictionary<string, int> ByMunicipality { get; init; } =
        new SortedDictionary<string, int>(StringComparer.Ordinal);
}
