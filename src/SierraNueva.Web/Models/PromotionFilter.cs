using SierraNueva.Contracts;

namespace SierraNueva.Web.Models;

public sealed class PromotionFilter
{
    public string Search { get; set; } = string.Empty;

    public HashSet<string> Municipalities { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Locality { get; set; } = string.Empty;

    public string PropertyType { get; set; } = string.Empty;

    public decimal? PriceMin { get; set; }

    public decimal? PriceMax { get; set; }

    public int? BedroomsMin { get; set; }

    public decimal? BuiltAreaMin { get; set; }

    public decimal? PlotAreaMin { get; set; }

    public string CommercialStatus { get; set; } = string.Empty;

    public string ConstructionStatus { get; set; } = string.Empty;

    public string SourceKind { get; set; } = string.Empty;

    public decimal? ConfidenceMin { get; set; }

    public bool ActiveOnly { get; set; } = true;

    public bool WithPriceOnly { get; set; }

    public bool ExactLocationOnly { get; set; }

    public bool NewOnly { get; set; }

    public bool ChangedOnly { get; set; }

    public string Sort { get; set; } = "recent";

    public IReadOnlyList<Promotion> Apply(
        IEnumerable<Promotion> source,
        IReadOnlySet<string> newIds,
        IReadOnlySet<string> changedIds)
    {
        IEnumerable<Promotion> query = source;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            string search = Search.Trim();
            query = query.Where(item =>
                Contains(item.Name, search) ||
                Contains(item.Municipality, search) ||
                Contains(item.Locality, search) ||
                Contains(item.DeveloperName, search) ||
                item.PropertyTypes.Any(value => Contains(value, search)));
        }

        if (Municipalities.Count > 0)
        {
            query = query.Where(item => Municipalities.Contains(item.Municipality));
        }

        if (!string.IsNullOrWhiteSpace(Locality))
        {
            query = query.Where(item => string.Equals(
                item.Locality,
                Locality,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(PropertyType))
        {
            query = query.Where(item => item.PropertyTypes.Any(value =>
                value.Contains(PropertyType, StringComparison.OrdinalIgnoreCase)));
        }

        if (PriceMin.HasValue)
        {
            query = query.Where(item =>
                item.PriceTo.GetValueOrDefault(item.PriceFrom ?? 0) >= PriceMin);
        }

        if (PriceMax.HasValue)
        {
            query = query.Where(item => item.PriceFrom.HasValue && item.PriceFrom <= PriceMax);
        }

        if (BedroomsMin.HasValue)
        {
            query = query.Where(item =>
                item.BedroomsMax.GetValueOrDefault(item.BedroomsMin ?? 0) >= BedroomsMin);
        }

        if (BuiltAreaMin.HasValue)
        {
            query = query.Where(item =>
                item.BuiltAreaMaxSqm.GetValueOrDefault(item.BuiltAreaMinSqm ?? 0) >= BuiltAreaMin);
        }

        if (PlotAreaMin.HasValue)
        {
            query = query.Where(item =>
                item.PlotAreaMaxSqm.GetValueOrDefault(item.PlotAreaMinSqm ?? 0) >= PlotAreaMin);
        }

        if (!string.IsNullOrWhiteSpace(CommercialStatus))
        {
            query = query.Where(item => item.CommercialStatus.ToString() == CommercialStatus);
        }

        if (!string.IsNullOrWhiteSpace(ConstructionStatus))
        {
            query = query.Where(item => item.ConstructionStatus.ToString() == ConstructionStatus);
        }

        if (!string.IsNullOrWhiteSpace(SourceKind))
        {
            query = query.Where(item => item.SourceKind.ToString() == SourceKind);
        }

        if (ConfidenceMin.HasValue)
        {
            query = query.Where(item => item.SourceConfidence >= ConfidenceMin);
        }

        if (ActiveOnly)
        {
            query = query.Where(item => item.Active);
        }

        if (WithPriceOnly)
        {
            query = query.Where(item => item.PriceFrom.HasValue);
        }

        if (ExactLocationOnly)
        {
            query = query.Where(item =>
                item.LocationPrecision == LocationPrecision.ExactCoordinates);
        }

        if (NewOnly)
        {
            query = query.Where(item => newIds.Contains(item.Id));
        }

        if (ChangedOnly)
        {
            query = query.Where(item => changedIds.Contains(item.Id));
        }

        return Sort switch
        {
            "price-asc" => query.OrderBy(item => item.PriceFrom ?? decimal.MaxValue).ToArray(),
            "price-desc" => query.OrderByDescending(item => item.PriceFrom ?? decimal.MinValue).ToArray(),
            "plot-desc" => query.OrderByDescending(item => item.PlotAreaMaxSqm ?? item.PlotAreaMinSqm).ToArray(),
            "area-desc" => query.OrderByDescending(item => item.BuiltAreaMaxSqm ?? item.BuiltAreaMinSqm).ToArray(),
            "confidence" => query.OrderByDescending(item => item.SourceConfidence).ToArray(),
            "municipality" => query
                .OrderBy(item => item.Municipality, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => query.OrderByDescending(item => item.LastChangedUtc)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public void Reset()
    {
        Search = string.Empty;
        Municipalities.Clear();
        Locality = string.Empty;
        PropertyType = string.Empty;
        PriceMin = null;
        PriceMax = null;
        BedroomsMin = null;
        BuiltAreaMin = null;
        PlotAreaMin = null;
        CommercialStatus = string.Empty;
        ConstructionStatus = string.Empty;
        SourceKind = string.Empty;
        ConfidenceMin = null;
        ActiveOnly = true;
        WithPriceOnly = false;
        ExactLocationOnly = false;
        NewOnly = false;
        ChangedOnly = false;
        Sort = "recent";
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }
}
