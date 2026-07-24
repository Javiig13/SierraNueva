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
    public string SchemaVersion { get; init; } = "1.0";

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public IReadOnlyList<PromotionEnrichment> Items { get; init; } = [];
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

public sealed class EnrichmentRunResult
{
    public int EligiblePromotions { get; init; }

    public int ProcessedPromotions { get; init; }

    public int CachedPromotions { get; init; }

    public int ProposedFields { get; init; }

    public int FailedPromotions { get; init; }

    public bool DryRun { get; init; }
}
