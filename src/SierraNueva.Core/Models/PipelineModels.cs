using SierraNueva.Contracts;

namespace SierraNueva.Core.Models;

public sealed record FetchedPage(
    Uri Url,
    string Content,
    string ContentType,
    DateTimeOffset FetchedAtUtc,
    string ExtractorHint = "http");

public sealed class PageBatch
{
    public IReadOnlyList<FetchedPage> Pages { get; init; } = [];

    public int DiscoveredUrls { get; init; }

    public int SkippedUrls { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class CrawlRequest
{
    public IReadOnlyList<SourceDefinition> Sources { get; init; } = [];

    public IReadOnlyList<MunicipalityDefinition> Municipalities { get; init; } = [];

    public CrawlerSettings Settings { get; init; } = new();

    public string OutputDirectory { get; init; } = string.Empty;

    public string StateDirectory { get; init; } = string.Empty;

    public string? SourceFilter { get; init; }

    public string? MunicipalityFilter { get; init; }

    public int? MaxPages { get; init; }

    public bool DisablePlaywright { get; init; }

    public bool DisableGeocoding { get; init; }

    public bool DryRun { get; init; }
}

public sealed class CrawlResult
{
    public PromotionDataset Dataset { get; init; } = new();

    public ChangeDataset Changes { get; init; } = new();

    public RunReport Run { get; init; } = new();

    public bool HasPublishableData { get; init; }
}

public sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; } = [];

    public List<string> Warnings { get; } = [];
}
