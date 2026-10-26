using System.Text.Json;
using SierraNueva.Contracts;

namespace SierraNueva.Web.Models;

public sealed class SiteData
{
    public PromotionDataset Promotions { get; init; } = new();

    public ChangeDataset Changes { get; init; } = new();

    public RunReport Run { get; init; } = new();

    public JsonElement GeoJson { get; init; }

    public bool IsStale(DateTimeOffset now)
    {
        int threshold = Run.DataFreshness.StaleAfterHours;
        return Run.DataFreshness.IsStale ||
               UiFormatting.IsStale(Promotions.GeneratedAtUtc, threshold, now) ||
               (Run.DataFreshness.NewestSourceCheckUtc is { } checkedAt &&
                UiFormatting.IsStale(checkedAt, threshold, now));
    }
}
