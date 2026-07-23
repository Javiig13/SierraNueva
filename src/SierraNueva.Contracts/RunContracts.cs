namespace SierraNueva.Contracts;

public sealed class RunReport
{
    public string SchemaVersion { get; init; } = "1.0";

    public string RunId { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public double DurationSeconds { get; init; }

    public RunStatus Status { get; init; }

    public string CrawlerVersion { get; init; } = string.Empty;

    public int TotalSources { get; init; }

    public int SuccessfulSources { get; init; }

    public int FailedSources { get; init; }

    public int SkippedSources { get; init; }

    public int DiscoveredUrls { get; init; }

    public int FetchedUrls { get; init; }

    public int SkippedUrls { get; init; }

    public int ExtractedPromotions { get; init; }

    public int NewPromotions { get; init; }

    public int ChangedPromotions { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<SourceRunResult> SourceResults { get; init; } = [];

    public DataFreshness DataFreshness { get; init; } = new();

    public DataQualityReport Quality { get; init; } = new();
}

public sealed class SourceRunResult
{
    public string SourceId { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public SourceRunStatus Status { get; init; }

    public int UrlsDiscovered { get; init; }

    public int UrlsFetched { get; init; }

    public int UrlsSkipped { get; init; }

    public int PromotionsExtracted { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class DataFreshness
{
    public DateTimeOffset? OldestSourceCheckUtc { get; init; }

    public DateTimeOffset? NewestSourceCheckUtc { get; init; }

    public bool IsStale { get; init; }

    public int StaleAfterHours { get; init; } = 96;
}

public sealed class DataQualityReport
{
    public int ValidPromotions { get; init; }

    public int InvalidPromotions { get; init; }

    public int Warnings { get; init; }

    public IReadOnlyList<string> Issues { get; init; } = [];
}
