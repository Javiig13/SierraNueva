namespace SierraNueva.Core.Models;

public enum EnrichmentReviewStatus
{
    Pending,
    Accepted,
    Rejected,
    Stale
}

public sealed class EnrichmentState
{
    public string SchemaVersion { get; init; } = "1.2";

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public IReadOnlyList<PromotionEnrichment> Items { get; init; } = [];

    public IReadOnlyList<EnrichmentRunAudit> Runs { get; init; } = [];
}

public sealed class PromotionEnrichment
{
    public string Id { get; init; } = string.Empty;

    public string PromotionId { get; init; } = string.Empty;

    public string PromotionName { get; init; } = string.Empty;

    public string CanonicalUrl { get; init; } = string.Empty;

    public string ContentHash { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public DateTimeOffset EvidenceFetchedAtUtc { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public EnrichmentReviewStatus Status { get; init; } = EnrichmentReviewStatus.Pending;

    public DateTimeOffset? ReviewedAtUtc { get; init; }

    public EnrichmentUsage Usage { get; init; } = new();

    public decimal MaximumCostEstimateUsd { get; init; }

    public IReadOnlyList<EnrichmentFieldProposal> Fields { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class EnrichmentFieldProposal
{
    public string Field { get; init; } = string.Empty;

    public string ValueText { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public string EvidenceText { get; init; } = string.Empty;

    public decimal Confidence { get; init; }

    public EnrichmentReviewStatus Status { get; init; } = EnrichmentReviewStatus.Pending;

    public DateTimeOffset? ReviewedAtUtc { get; init; }
}

public sealed class EnrichmentEvidenceDocument
{
    public string PromotionId { get; init; } = string.Empty;

    public string PromotionName { get; init; } = string.Empty;

    public string Municipality { get; init; } = string.Empty;

    public string CanonicalUrl { get; init; } = string.Empty;

    public string ContentHash { get; init; } = string.Empty;

    public DateTimeOffset FetchedAtUtc { get; init; }

    public IReadOnlyList<EnrichmentEvidencePage> Pages { get; init; } = [];
}

public sealed class EnrichmentEvidencePage
{
    public string Url { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;
}

public sealed class EnrichmentProviderResult
{
    public IReadOnlyList<EnrichmentFieldProposal> Fields { get; init; } = [];

    public EnrichmentUsage Usage { get; init; } = new();
}

public sealed class EnrichmentUsage
{
    public int InputTokens { get; init; }

    public int CachedInputTokens { get; init; }

    public int CacheWriteTokens { get; init; }

    public int OutputTokens { get; init; }

    public int ReasoningTokens { get; init; }

    public int TotalTokens { get; init; }

    public decimal EstimatedCostUsd { get; init; }
}

public sealed class EnrichmentCostEstimate
{
    public int MaximumInputTokens { get; init; }

    public int MaximumOutputTokens { get; init; }

    public decimal MaximumCostUsd { get; init; }
}

public sealed class EnrichmentRunOptions
{
    public string? PromotionFilter { get; init; }

    public int MaxPromotions { get; init; } = 3;

    public int MaxEvidencePages { get; init; } = 3;

    public int MaxEvidenceCharacters { get; init; } = 8_000;

    public decimal MaxCostUsd { get; init; } = 0.05m;

    public bool DryRun { get; init; }
}

public sealed class EnrichmentRunAudit
{
    public string Id { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int MaxPromotions { get; init; }

    public int MaxEvidencePages { get; init; }

    public int MaxEvidenceCharacters { get; init; }

    public int MaxOutputTokens { get; init; }

    public decimal MaxCostUsd { get; init; }

    public int ProcessedPromotions { get; init; }

    public int CachedPromotions { get; init; }

    public int FailedPromotions { get; init; }

    public int BudgetSkippedPromotions { get; init; }

    public EnrichmentUsage Usage { get; init; } = new();
}

public sealed class EnrichmentRunResult
{
    public int EligiblePromotions { get; init; }

    public int ProcessedPromotions { get; init; }

    public int CachedPromotions { get; init; }

    public int PlannedPromotions { get; init; }

    public int BudgetSkippedPromotions { get; init; }

    public int ProposedFields { get; init; }

    public int FailedPromotions { get; init; }

    public decimal ReservedMaximumCostUsd { get; init; }

    public EnrichmentUsage Usage { get; init; } = new();

    public bool DryRun { get; init; }
}
