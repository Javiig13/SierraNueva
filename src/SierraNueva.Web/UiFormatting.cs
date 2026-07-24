using System.Globalization;
using SierraNueva.Contracts;

namespace SierraNueva.Web;

public static class UiFormatting
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public static string Price(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("C0", Spanish) : "Precio no publicado";
    }

    public static string Range(decimal? minimum, decimal? maximum, string suffix)
    {
        return (minimum, maximum) switch
        {
            (null, null) => "—",
            ({ } min, { } max) when min != max =>
                $"{min.ToString("N0", Spanish)}–{max.ToString("N0", Spanish)} {suffix}",
            ({ } min, _) => $"{min.ToString("N0", Spanish)} {suffix}",
            _ => $"Hasta {maximum!.Value.ToString("N0", Spanish)} {suffix}"
        };
    }

    public static string Date(DateTimeOffset value)
    {
        TimeZoneInfo zone = GetMadridTimeZone();
        return TimeZoneInfo.ConvertTime(value, zone).ToString("d MMM yyyy", Spanish);
    }

    public static string Commercial(CommercialStatus status)
    {
        return status switch
        {
            CommercialStatus.Announced => "Anunciada",
            CommercialStatus.Upcoming => "Próximamente",
            CommercialStatus.PreSales => "Preventa",
            CommercialStatus.OnSale => "En venta",
            CommercialStatus.LastUnits => "Últimas unidades",
            CommercialStatus.SoldOut => "Agotada",
            CommercialStatus.Completed => "Finalizada",
            CommercialStatus.Paused => "Pausada",
            _ => "Sin confirmar"
        };
    }

    public static string Construction(ConstructionStatus status)
    {
        return status switch
        {
            ConstructionStatus.Planned => "En proyecto",
            ConstructionStatus.Licensed => "Con licencia",
            ConstructionStatus.UnderConstruction => "En construcción",
            ConstructionStatus.Completed => "Terminada",
            _ => "Sin confirmar"
        };
    }

    public static string Source(SourceKind kind)
    {
        return kind switch
        {
            SourceKind.OfficialPromoter => "Promotora oficial",
            SourceKind.OfficialMicrosite => "Micrositio oficial",
            SourceKind.CooperativeManager => "Gestora de cooperativa",
            SourceKind.ExclusiveMarketer => "Comercializadora exclusiva",
            SourceKind.Builder => "Constructora",
            SourceKind.PublicAuthority => "Fuente pública",
            _ => "Sin clasificar"
        };
    }

    public static string Location(LocationPrecision precision)
    {
        return precision switch
        {
            LocationPrecision.ExactCoordinates => "Ubicación exacta",
            LocationPrecision.Street => "Calle aproximada",
            LocationPrecision.DevelopmentArea => "Zona de desarrollo",
            LocationPrecision.Locality => "Centro de localidad",
            LocationPrecision.MunicipalityCentroid => "Centro municipal aproximado",
            _ => "Sin ubicación"
        };
    }

    public static int Completeness(Promotion promotion)
    {
        int populated = 0;
        populated += string.IsNullOrWhiteSpace(promotion.DeveloperName) ? 0 : 1;
        populated += promotion.PriceFrom.HasValue ? 1 : 0;
        populated += promotion.BedroomsMin.HasValue || promotion.BedroomsMax.HasValue ? 1 : 0;
        populated += promotion.BathroomsMin.HasValue || promotion.BathroomsMax.HasValue ? 1 : 0;
        populated += promotion.BuiltAreaMinSqm.HasValue || promotion.BuiltAreaMaxSqm.HasValue ? 1 : 0;
        populated += promotion.PlotAreaMinSqm.HasValue || promotion.PlotAreaMaxSqm.HasValue ? 1 : 0;
        populated += promotion.PropertyTypes.Count > 0 ? 1 : 0;
        populated += promotion.AvailableUnits.HasValue ? 1 : 0;
        populated += string.IsNullOrWhiteSpace(promotion.Address) ? 0 : 1;
        populated += string.IsNullOrWhiteSpace(promotion.DeliveryDateText) ? 0 : 1;
        populated += promotion.LocationPrecision == LocationPrecision.ExactCoordinates ? 1 : 0;
        return (int)Math.Round(populated / 11m * 100m, MidpointRounding.AwayFromZero);
    }

    public static string CompletenessTitle(Promotion promotion)
    {
        return $"{Completeness(promotion)}% de los campos principales publicados por la fuente";
    }

    private static TimeZoneInfo GetMadridTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}
