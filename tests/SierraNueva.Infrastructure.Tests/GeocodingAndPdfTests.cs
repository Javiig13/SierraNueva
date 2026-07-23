using SierraNueva.Contracts;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Documents;
using SierraNueva.Infrastructure.Extraction;
using SierraNueva.Infrastructure.Geocoding;

namespace SierraNueva.Infrastructure.Tests;

public sealed class GeocodingAndPdfTests
{
    [Fact]
    public async Task CentroidGeocoder_MarksApproximateLocation()
    {
        Promotion promotion = new()
        {
            Name = "Residencial",
            Municipality = "Soto del Real",
            CanonicalUrl = "https://example.com/promo"
        };

        Promotion result = await new MunicipalityCentroidGeocoder().GeocodeAsync(
            promotion,
            [
                new MunicipalityDefinition
                {
                    OfficialName = "Soto del Real",
                    Latitude = 40.754,
                    Longitude = -3.783
                }
            ],
            CancellationToken.None);

        Assert.Equal(LocationPrecision.MunicipalityCentroid, result.LocationPrecision);
        Assert.Equal(40.754, result.Latitude);
        Assert.Contains(result.Warnings, warning => warning.Contains("aproximada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PdfFixture_ExtractsCommercialPromotion()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "pdfs",
            "residencial-cumbre-fixture.pdf");
        byte[] content = await File.ReadAllBytesAsync(path);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(content, 0, 4));
        string text = new PdfPigTextExtractor().Extract(content);
        IReadOnlyList<Promotion> promotions = await new LayeredPromotionExtractor().ExtractAsync(
            new FetchedPage(
                new("https://fixtures.sierranueva.test/residencial-cumbre.pdf"),
                text,
                "text/plain",
                new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero),
                "pdf"),
            new SourceDefinition
            {
                Id = "pdf-fixture",
                SourceKind = SourceKind.OfficialPromoter,
                MunicipalityHints = ["Moralzarzal"]
            },
            [new MunicipalityDefinition { OfficialName = "Moralzarzal" }],
            CancellationToken.None);

        Assert.Contains("Residencial Cumbre", text, StringComparison.Ordinal);
        Assert.Contains("475.000 EUR", text, StringComparison.Ordinal);
        Promotion promotion = Assert.Single(promotions);
        Assert.Equal("Residencial Cumbre", promotion.Name);
        Assert.Equal("Moralzarzal", promotion.Municipality);
        Assert.Equal(475_000m, promotion.PriceFrom);
        Assert.Contains(
            "https://fixtures.sierranueva.test/residencial-cumbre.pdf",
            promotion.BrochureUrls);
    }
}
