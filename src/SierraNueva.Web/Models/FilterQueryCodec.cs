namespace SierraNueva.Web.Models;

public static class FilterQueryCodec
{
    public static string Build(PromotionFilter filter)
    {
        List<KeyValuePair<string, string>> values = [];
        Add(values, "q", filter.Search);
        Add(
            values,
            "municipios",
            string.Join(',', filter.Municipalities.Order(StringComparer.OrdinalIgnoreCase)));
        Add(values, "localidad", filter.Locality);
        Add(values, "tipo", filter.PropertyType);
        Add(values, "precioMin", filter.PriceMin);
        Add(values, "precioMax", filter.PriceMax);
        Add(values, "dormitorios", filter.BedroomsMin);
        Add(values, "superficie", filter.BuiltAreaMin);
        Add(values, "parcela", filter.PlotAreaMin);
        Add(values, "comercial", filter.CommercialStatus);
        Add(values, "construccion", filter.ConstructionStatus);
        Add(values, "fuente", filter.SourceKind);
        Add(values, "confianza", filter.ConfidenceMin);
        Add(values, "activas", filter.ActiveOnly ? null : "false");
        Add(values, "conPrecio", filter.WithPriceOnly ? "true" : null);
        Add(values, "exacta", filter.ExactLocationOnly ? "true" : null);
        Add(values, "nuevas", filter.NewOnly ? "true" : null);
        Add(values, "cambios", filter.ChangedOnly ? "true" : null);
        Add(values, "orden", filter.Sort == "recent" ? null : filter.Sort);
        return string.Join(
            "&",
            values.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    public static void Apply(Uri uri, PromotionFilter filter)
    {
        Dictionary<string, string> values = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);
        filter.Search = Get(values, "q");
        foreach (string municipality in Get(values, "municipios")
                     .Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
        {
            filter.Municipalities.Add(municipality);
        }

        filter.Locality = Get(values, "localidad");
        filter.PropertyType = Get(values, "tipo");
        filter.PriceMin = ParseDecimal(values, "precioMin");
        filter.PriceMax = ParseDecimal(values, "precioMax");
        filter.BedroomsMin = ParseInt(values, "dormitorios");
        filter.BuiltAreaMin = ParseDecimal(values, "superficie");
        filter.PlotAreaMin = ParseDecimal(values, "parcela");
        filter.CommercialStatus = Get(values, "comercial");
        filter.ConstructionStatus = Get(values, "construccion");
        filter.SourceKind = Get(values, "fuente");
        filter.ConfidenceMin = ParseDecimal(values, "confianza");
        filter.ActiveOnly = Get(values, "activas") != "false";
        filter.WithPriceOnly = Get(values, "conPrecio") == "true";
        filter.ExactLocationOnly = Get(values, "exacta") == "true";
        filter.NewOnly = Get(values, "nuevas") == "true";
        filter.ChangedOnly = Get(values, "cambios") == "true";
        filter.Sort = Get(values, "orden") is { Length: > 0 } sort ? sort : "recent";
    }

    private static void Add(
        ICollection<KeyValuePair<string, string>> values,
        string key,
        object? value)
    {
        string? text = value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(
                null,
                System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        if (!string.IsNullOrWhiteSpace(text))
        {
            values.Add(new(key, text));
        }
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string? value) ? value : string.Empty;
    }

    private static decimal? ParseDecimal(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return decimal.TryParse(
            Get(values, key),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal value)
            ? value
            : null;
    }

    private static int? ParseInt(IReadOnlyDictionary<string, string> values, string key)
    {
        return int.TryParse(Get(values, key), out int value) ? value : null;
    }
}
