using SierraNueva.Contracts;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Core.Quality;

public sealed class PromotionValidator
{
    public ValidationResult Validate(
        Promotion promotion,
        IReadOnlyList<MunicipalityDefinition> municipalities)
    {
        ValidationResult result = new();

        if (string.IsNullOrWhiteSpace(promotion.Name))
        {
            result.Errors.Add("La promoción no tiene nombre.");
        }

        if (!Uri.TryCreate(promotion.CanonicalUrl, UriKind.Absolute, out Uri? sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            result.Errors.Add("La promoción no tiene una URL de origen HTTP(S) válida.");
        }

        ValidateRange(promotion.PriceFrom, promotion.PriceTo, "price", result);
        ValidateRange(promotion.BuiltAreaMinSqm, promotion.BuiltAreaMaxSqm, "builtArea", result);
        ValidateRange(promotion.PlotAreaMinSqm, promotion.PlotAreaMaxSqm, "plotArea", result);
        ValidateRange(promotion.BedroomsMin, promotion.BedroomsMax, "bedrooms", result);

        if (promotion.PriceFrom < 0 || promotion.PriceTo < 0)
        {
            result.Errors.Add("Los precios no pueden ser negativos.");
        }

        if (promotion.Latitude is < -90 or > 90 || promotion.Longitude is < -180 or > 180)
        {
            result.Errors.Add("Las coordenadas están fuera de rango.");
        }

        bool knownMunicipality = municipalities.Any(item =>
            TextNormalizer.NormalizeForComparison(item.OfficialName) ==
            TextNormalizer.NormalizeForComparison(promotion.Municipality));
        if (!knownMunicipality)
        {
            result.Warnings.Add($"Municipio fuera de configuración: {promotion.Municipality}.");
        }

        if (promotion.LocationPrecision == LocationPrecision.MunicipalityCentroid &&
            (promotion.Latitude is null || promotion.Longitude is null))
        {
            result.Errors.Add("Una ubicación por centroide debe incluir coordenadas.");
        }

        return result;
    }

    private static void ValidateRange<T>(T? minimum, T? maximum, string field, ValidationResult result)
        where T : struct, IComparable<T>
    {
        if (minimum.HasValue && maximum.HasValue && maximum.Value.CompareTo(minimum.Value) < 0)
        {
            result.Errors.Add($"{field}Max no puede ser menor que {field}Min.");
        }
    }
}
