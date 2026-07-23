using System.Text.Json;
using SierraNueva.Contracts;

namespace SierraNueva.Web.Models;

public sealed class SiteData
{
    public PromotionDataset Promotions { get; init; } = new();

    public ChangeDataset Changes { get; init; } = new();

    public RunReport Run { get; init; } = new();

    public JsonElement GeoJson { get; init; }
}
