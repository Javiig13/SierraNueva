namespace SierraNueva.Contracts;

public sealed class CrawlerSettings
{
    public string UserAgent { get; init; } =
        "SierraNueva/1.0 (+https://localhost; contacto: local@example.invalid)";

    public int MaxConcurrencyGlobal { get; init; } = 4;

    public int MaxConcurrencyPerHost { get; init; } = 1;

    public int RequestDelayMilliseconds { get; init; } = 2_000;

    public int MaxPagesPerSource { get; init; } = 100;

    public int MaxDepth { get; init; } = 3;

    public int TimeoutSeconds { get; init; } = 30;

    public int MaxRetries { get; init; } = 2;

    public int MaxResponseBytes { get; init; } = 5_000_000;

    public int MaxPdfBytes { get; init; } = 15_000_000;

    public int DeactivateAfterMisses { get; init; } = 3;

    public int PublicChangeLimit { get; init; } = 5_000;

    public int StaleAfterHours { get; init; } = 96;

    public bool EnablePlaywright { get; init; }

    public NominatimSettings Nominatim { get; init; } = new();

    public MapSettings Map { get; init; } = new();
}

public sealed class NominatimSettings
{
    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = "https://nominatim.openstreetmap.org";

    public int MaxRequestsPerMinute { get; init; } = 4;
}

public sealed class MapSettings
{
    public string TileUrl { get; init; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    public string Attribution { get; init; } = "© OpenStreetMap contributors";
}

public sealed class SourceDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public SourceKind SourceKind { get; init; }

    public IReadOnlyList<string> AllowedHosts { get; init; } = [];

    public IReadOnlyList<string> StartUrls { get; init; } = [];

    public bool UseRobots { get; init; } = true;

    public bool UseSitemaps { get; init; } = true;

    public bool FollowInternalLinks { get; init; }

    public int MaxDepth { get; init; } = 3;

    public int MaxPages { get; init; } = 100;

    public int RequestDelayMilliseconds { get; init; } = 2_000;

    public bool UsePlaywright { get; init; }

    public IReadOnlyList<string> MunicipalityHints { get; init; } = [];

    public IReadOnlyList<string> IncludePatterns { get; init; } = [];

    public IReadOnlyList<string> ExcludePatterns { get; init; } = [];

    public IReadOnlyDictionary<string, string> CustomSelectors { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? ManualUrlsFile { get; init; }

    public string? FixturePath { get; init; }

    public string? Notes { get; init; }
}

public sealed class MunicipalityDefinition
{
    public string OfficialName { get; init; } = string.Empty;

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public IReadOnlyList<string> Localities { get; init; } = [];

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public IReadOnlyList<double>? BoundingBox { get; init; }

    public bool Enabled { get; init; } = true;

    public IReadOnlyList<string> SearchTerms { get; init; } = [];
}

public sealed class DomainExclusions
{
    public IReadOnlyList<string> Domains { get; init; } = [];
}
