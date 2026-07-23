namespace SierraNueva.Contracts;

public sealed class ChangeDataset
{
    public string SchemaVersion { get; init; } = "1.0";

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public string RunId { get; init; } = string.Empty;

    public IReadOnlyList<PromotionChange> Changes { get; init; } = [];
}

public sealed class PromotionChange
{
    public string Id { get; init; } = string.Empty;

    public string PromotionId { get; init; } = string.Empty;

    public string PromotionName { get; init; } = string.Empty;

    public ChangeKind Kind { get; init; }

    public DateTimeOffset DetectedAtUtc { get; init; }

    public IReadOnlyList<FieldChange> Fields { get; init; } = [];
}

public sealed class FieldChange
{
    public string Field { get; init; } = string.Empty;

    public string? PreviousValue { get; init; }

    public string? CurrentValue { get; init; }
}
