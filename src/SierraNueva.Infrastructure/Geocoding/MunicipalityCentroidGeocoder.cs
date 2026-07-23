using System.Globalization;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Infrastructure.Geocoding;

public sealed class MunicipalityCentroidGeocoder : IGeocoder
{
    public Task<Promotion> GeocodeAsync(
        Promotion promotion,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (promotion.Latitude.HasValue && promotion.Longitude.HasValue)
        {
            if (promotion.LocationPrecision == LocationPrecision.Unknown)
            {
                promotion.LocationPrecision = LocationPrecision.ExactCoordinates;
            }

            return Task.FromResult(promotion);
        }

        MunicipalityDefinition? municipality = municipalities.FirstOrDefault(item =>
            TextNormalizer.NormalizeForComparison(item.OfficialName) ==
            TextNormalizer.NormalizeForComparison(promotion.Municipality));
        if (municipality?.Latitude is not null && municipality.Longitude is not null)
        {
            promotion.Latitude = municipality.Latitude;
            promotion.Longitude = municipality.Longitude;
            promotion.LocationPrecision = LocationPrecision.MunicipalityCentroid;
            promotion.Warnings = promotion.Warnings.Append(
                "Ubicación aproximada: se muestra el centroide municipal.").ToArray();
            promotion.Evidence = promotion.Evidence.Append(new EvidenceItem
            {
                Field = "location",
                ValueText = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{municipality.Latitude}, {municipality.Longitude}"),
                SourceUrl = promotion.CanonicalUrl,
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Extractor = nameof(MunicipalityCentroidGeocoder),
                Confidence = 0.55m,
                Quality = FieldQuality.Approximate,
                TextFragment = $"Centroide configurado de {municipality.OfficialName}"
            }).ToArray();
        }

        return Task.FromResult(promotion);
    }
}
