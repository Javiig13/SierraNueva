using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SierraNueva.Contracts;

namespace SierraNueva.Core.Changes;

public sealed class ChangeDetector
{
    private static readonly IReadOnlyList<(string Name, Func<Promotion, object?> Value)> TrackedFields =
    [
        ("priceFrom", promotion => promotion.PriceFrom),
        ("priceTo", promotion => promotion.PriceTo),
        ("commercialStatus", promotion => promotion.CommercialStatus),
        ("constructionStatus", promotion => promotion.ConstructionStatus),
        ("availableUnits", promotion => promotion.AvailableUnits),
        ("deliveryDateText", promotion => promotion.DeliveryDateText),
        ("builtAreaMinSqm", promotion => promotion.BuiltAreaMinSqm),
        ("builtAreaMaxSqm", promotion => promotion.BuiltAreaMaxSqm),
        ("plotAreaMinSqm", promotion => promotion.PlotAreaMinSqm),
        ("plotAreaMaxSqm", promotion => promotion.PlotAreaMaxSqm),
        ("active", promotion => promotion.Active)
    ];

    public PromotionChange? Detect(Promotion? previous, Promotion current, DateTimeOffset now)
    {
        if (previous is null)
        {
            return Create(current, ChangeKind.Added, now, []);
        }

        List<FieldChange> fields = [];
        foreach ((string name, Func<Promotion, object?> value) in TrackedFields)
        {
            string? previousValue = Format(value(previous));
            string? currentValue = Format(value(current));
            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                fields.Add(new()
                {
                    Field = name,
                    PreviousValue = previousValue,
                    CurrentValue = currentValue
                });
            }
        }

        if (fields.Count == 0)
        {
            return null;
        }

        ChangeKind kind = !previous.Active && current.Active
            ? ChangeKind.Reactivated
            : previous.Active && !current.Active
                ? ChangeKind.Deactivated
                : ChangeKind.Updated;
        return Create(current, kind, now, fields);
    }

    private static PromotionChange Create(
        Promotion promotion,
        ChangeKind kind,
        DateTimeOffset now,
        IReadOnlyList<FieldChange> fields)
    {
        string rawId = $"{promotion.Id}|{kind}|{now:O}";
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawId)).AsSpan(0, 8))
            .ToLowerInvariant();

        return new()
        {
            Id = $"change-{id}",
            PromotionId = promotion.Id,
            PromotionName = promotion.Name,
            Kind = kind,
            DetectedAtUtc = now,
            Fields = fields
        };
    }

    private static string? Format(object? value)
    {
        return value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
