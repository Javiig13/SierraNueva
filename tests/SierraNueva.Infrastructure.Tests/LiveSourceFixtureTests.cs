using SierraNueva.Contracts;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Configuration;
using SierraNueva.Infrastructure.Extraction;

namespace SierraNueva.Infrastructure.Tests;

public sealed class LiveSourceFixtureTests
{
    public static TheoryData<string, string> ReviewedSources =>
        new()
        {
            { "apremya-puerta-villalba", "apremya-puerta-villalba.html" },
            { "altter-navata-nature", "altter-navata-nature.html" },
            { "los-pinarejos-p6", "los-pinarejos-p6.html" },
            { "trinosa-etria", "trinosa-etria.html" },
            { "kronos-onix", "kronos-onix.html" },
            { "quinta-manzanares", "quinta-manzanares.html" },
            { "gilmar-quercus-dorf", "gilmar-quercus-dorf.html" }
        };

    [Theory]
    [MemberData(nameof(ReviewedSources))]
    public async Task ReviewedLiveSource_ExtractsReducedFixture(
        string sourceId,
        string fixtureName)
    {
        ConfigurationLoader loader = new();
        IReadOnlyList<SourceDefinition> sources = await loader.LoadSourcesAsync(
            Path.Combine(AppContext.BaseDirectory, "config", "sources.live.json"),
            CancellationToken.None);
        IReadOnlyList<MunicipalityDefinition> municipalities =
            await loader.LoadMunicipalitiesAsync(
                Path.Combine(AppContext.BaseDirectory, "config", "municipalities.json"),
                CancellationToken.None);
        SourceDefinition source = Assert.Single(sources, item => item.Id == sourceId);
        string html = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "source-formats",
            fixtureName));

        Promotion promotion = Assert.Single(await new LayeredPromotionExtractor().ExtractAsync(
            new FetchedPage(
                new(source.StartUrls[0]),
                html,
                "text/html",
                new DateTimeOffset(2026, 7, 23, 16, 0, 0, TimeSpan.Zero),
                "fixture"),
            source,
            municipalities,
            CancellationToken.None));

        Assert.Equal(source.FixedMunicipality, promotion.Municipality);
        Assert.False(string.IsNullOrWhiteSpace(promotion.Name));
        Assert.Contains(
            promotion.Evidence,
            item => item.Extractor == "ReviewedSourceConfiguration");
        AssertSourceSpecificFields(sourceId, promotion);
    }

    private static void AssertSourceSpecificFields(string sourceId, Promotion promotion)
    {
        switch (sourceId)
        {
            case "apremya-puerta-villalba":
                Assert.Equal(10, promotion.TotalUnits);
                Assert.Equal(205m, promotion.BuiltAreaMinSqm);
                Assert.Equal(5, promotion.BedroomsMin);
                Assert.Equal(ConstructionStatus.UnderConstruction, promotion.ConstructionStatus);
                break;
            case "altter-navata-nature":
                Assert.Equal(4, promotion.TotalUnits);
                Assert.Equal(234m, promotion.BuiltAreaMinSqm);
                Assert.Equal(500m, promotion.PlotAreaMinSqm);
                Assert.Equal("Solicitada", promotion.BuildingLicenceStatus);
                break;
            case "los-pinarejos-p6":
                Assert.Equal(8, promotion.TotalUnits);
                Assert.Equal(545_000m, promotion.PriceFrom);
                Assert.Equal(565_000m, promotion.PriceTo);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(4, promotion.BedroomsMax);
                Assert.Equal(410m, promotion.PlotAreaMinSqm);
                Assert.Equal(500m, promotion.PlotAreaMaxSqm);
                break;
            case "trinosa-etria":
                Assert.Equal(13, promotion.TotalUnits);
                Assert.Equal(CommercialStatus.PreSales, promotion.CommercialStatus);
                Assert.Equal(3, promotion.BedroomsMin);
                break;
            case "kronos-onix":
                Assert.Equal(29, promotion.TotalUnits);
                Assert.Equal(1_130_000m, promotion.PriceFrom);
                Assert.Equal(1_430_000m, promotion.PriceTo);
                Assert.Equal(CommercialStatus.LastUnits, promotion.CommercialStatus);
                break;
            case "quinta-manzanares":
                Assert.Equal(500m, promotion.PlotAreaMinSqm);
                Assert.Null(promotion.PlotAreaMaxSqm);
                Assert.Equal(160m, promotion.BuiltAreaMinSqm);
                Assert.Contains("Independiente", promotion.PropertyTypes);
                break;
            case "gilmar-quercus-dorf":
                Assert.Equal(1, promotion.AvailableUnits);
                Assert.Equal(CommercialStatus.LastUnits, promotion.CommercialStatus);
                Assert.Equal(242m, promotion.BuiltAreaMinSqm);
                Assert.Equal(252m, promotion.BuiltAreaMaxSqm);
                Assert.Equal(811m, promotion.PlotAreaMinSqm);
                Assert.Equal(1026m, promotion.PlotAreaMaxSqm);
                break;
            default:
                throw new InvalidOperationException($"Fuente no cubierta: {sourceId}");
        }
    }
}
