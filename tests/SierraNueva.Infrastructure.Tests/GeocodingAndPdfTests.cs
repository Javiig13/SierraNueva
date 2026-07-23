using SierraNueva.Contracts;
using SierraNueva.Infrastructure.Documents;
using SierraNueva.Infrastructure.Geocoding;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

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
    public void PdfExtractor_ReadsCommercialText()
    {
        PdfDocumentBuilder builder = new();
        PdfPageBuilder page = builder.AddPage(PageSize.A4);
        PdfDocumentBuilder.AddedFont font =
            builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(
            "Residencial Cumbre - precio 475000 EUR",
            12,
            new PdfPoint(40, 750),
            font);

        string text = new PdfPigTextExtractor().Extract(builder.Build());

        Assert.Contains("Residencial Cumbre", text, StringComparison.Ordinal);
        Assert.Contains("475000 EUR", text, StringComparison.Ordinal);
    }
}
