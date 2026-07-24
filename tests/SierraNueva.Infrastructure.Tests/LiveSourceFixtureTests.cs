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
            { "gilmar-quercus-dorf", "gilmar-quercus-dorf.html" },
            { "antaro-prado-de-noria", "antaro-prado-de-noria.html" },
            { "antaro-los-trigales", "antaro-los-trigales.html" },
            { "grupo-index-sierra-bonita", "grupo-index-sierra-bonita.html" },
            { "vesari-el-tomillar", "vesari-el-tomillar.html" },
            { "vesari-cuarteto", "vesari-cuarteto.html" },
            { "vesari-luar-robledo", "vesari-luar-robledo.html" },
            {
                "residencial-alpedrete-la-bellota",
                "residencial-alpedrete-la-bellota.html"
            },
            {
                "hirimasa-moralzarzal-pradillos",
                "hirimasa-moralzarzal-pradillos.html"
            },
            {
                "nuvare-cumbres-navalafuente",
                "nuvare-cumbres-navalafuente.html"
            },
            {
                "nuvare-claveles-zarzalejo",
                "nuvare-claveles-zarzalejo.html"
            },
            {
                "stance-essentia-galapagar",
                "stance-essentia-galapagar.html"
            },
            {
                "stance-osnola-zarzalejo",
                "stance-osnola-zarzalejo.html"
            },
            {
                "residencial-montemilano-bustarviejo",
                "residencial-montemilano-bustarviejo.html"
            },
            {
                "nevola-homes-guadalix",
                "nevola-homes-guadalix.html"
            }
        };

    [Fact]
    public async Task VesariSources_KeepObservedSafeSharedHostDelay()
    {
        ConfigurationLoader loader = new();
        IReadOnlyList<SourceDefinition> sources = await loader.LoadSourcesAsync(
            Path.Combine(AppContext.BaseDirectory, "config", "sources.live.json"),
            CancellationToken.None);

        SourceDefinition[] vesariSources = sources
            .Where(source => source.AllowedHosts.Contains(
                "www.vesari.info",
                StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(3, vesariSources.Length);
        Assert.All(
            vesariSources,
            source => Assert.True(source.RequestDelayMilliseconds >= 30_000));
    }

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

    private static void AssertSourceSpecificFields(
        string sourceId,
        Promotion promotion)
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
            case "antaro-prado-de-noria":
                Assert.Equal(35, promotion.TotalUnits);
                Assert.Equal(479_500m, promotion.PriceFrom);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "antaro-los-trigales":
                Assert.Equal(25, promotion.TotalUnits);
                Assert.Equal(395_013m, promotion.PriceFrom);
                Assert.Equal(CommercialStatus.LastUnits, promotion.CommercialStatus);
                Assert.True(promotion.HasCommunityPool);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "grupo-index-sierra-bonita":
                Assert.Equal(80, promotion.TotalUnits);
                Assert.Equal(134m, promotion.BuiltAreaMinSqm);
                Assert.Equal(180m, promotion.BuiltAreaMaxSqm);
                Assert.Equal(250m, promotion.PlotAreaMinSqm);
                Assert.Equal(974m, promotion.PlotAreaMaxSqm);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(
                    ["Adosado", "Independiente", "Pareado"],
                    promotion.PropertyTypes);
                break;
            case "vesari-el-tomillar":
                Assert.Equal(18, promotion.TotalUnits);
                Assert.Contains("Pareado", promotion.PropertyTypes);
                break;
            case "vesari-cuarteto":
                Assert.Equal(4, promotion.TotalUnits);
                Assert.Equal(1, promotion.AvailableUnits);
                Assert.Equal(CommercialStatus.LastUnits, promotion.CommercialStatus);
                Assert.Contains("Adosado", promotion.PropertyTypes);
                break;
            case "vesari-luar-robledo":
                Assert.Equal(7, promotion.TotalUnits);
                break;
            case "residencial-alpedrete-la-bellota":
                Assert.Equal(9, promotion.TotalUnits);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.True(promotion.HasCommunityPool);
                Assert.Equal(
                    ["Independiente", "Pareado"],
                    promotion.PropertyTypes);
                break;
            case "hirimasa-moralzarzal-pradillos":
                Assert.Equal(13, promotion.TotalUnits);
                Assert.Equal(150m, promotion.BuiltAreaMinSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(2, promotion.BathroomsMin);
                Assert.True(promotion.HasCommunityPool);
                Assert.Equal(CommercialStatus.Unknown, promotion.CommercialStatus);
                Assert.Equal(
                    ["Adosado", "Pareado"],
                    promotion.PropertyTypes);
                break;
            case "nuvare-cumbres-navalafuente":
                Assert.Null(promotion.TotalUnits);
                Assert.Equal(2, promotion.AvailableUnits);
                Assert.Equal(512_500m, promotion.PriceFrom);
                Assert.Equal(543_000m, promotion.PriceTo);
                Assert.Equal(500m, promotion.PlotAreaMinSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Contains("Pareado", promotion.PropertyTypes);
                break;
            case "nuvare-claveles-zarzalejo":
                Assert.Equal(399_000m, promotion.PriceFrom);
                Assert.Equal(460_000m, promotion.PriceTo);
                Assert.Null(promotion.BuiltAreaMinSqm);
                Assert.Equal(150m, promotion.PlotAreaMinSqm);
                Assert.Equal(250m, promotion.PlotAreaMaxSqm);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.True(promotion.HasCommunityPool);
                break;
            case "stance-essentia-galapagar":
                Assert.Equal(4, promotion.TotalUnits);
                Assert.Equal(4, promotion.AvailableUnits);
                Assert.Equal(875_000m, promotion.PriceFrom);
                Assert.Equal(985_000m, promotion.PriceTo);
                Assert.Equal(340m, promotion.BuiltAreaMinSqm);
                Assert.Equal(500m, promotion.BuiltAreaMaxSqm);
                Assert.Equal(512m, promotion.PlotAreaMinSqm);
                Assert.Equal(1131m, promotion.PlotAreaMaxSqm);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.True(promotion.HasPrivatePool);
                Assert.Contains("Independiente", promotion.PropertyTypes);
                break;
            case "stance-osnola-zarzalejo":
                Assert.Equal(221m, promotion.BuiltAreaMinSqm);
                Assert.Equal(250m, promotion.PlotAreaMinSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(3, promotion.BathroomsMin);
                Assert.Equal("Concedida", promotion.BuildingLicenceStatus);
                Assert.Equal(ConstructionStatus.UnderConstruction, promotion.ConstructionStatus);
                Assert.Contains("Adosado", promotion.PropertyTypes);
                break;
            case "residencial-montemilano-bustarviejo":
                Assert.Equal(5, promotion.TotalUnits);
                Assert.Equal(106.45m, promotion.BuiltAreaMinSqm);
                Assert.Equal(425m, promotion.PlotAreaMinSqm);
                Assert.Equal(450m, promotion.PlotAreaMaxSqm);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Null(promotion.PriceFrom);
                break;
            case "nevola-homes-guadalix":
                Assert.Equal(16, promotion.TotalUnits);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(2, promotion.GarageSpacesMin);
                Assert.Equal(310m, promotion.PlotAreaMinSqm);
                Assert.Equal(319m, promotion.PlotAreaMaxSqm);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(
                    ["Independiente", "Pareado"],
                    promotion.PropertyTypes);
                break;
            default:
                throw new InvalidOperationException($"Fuente no cubierta: {sourceId}");
        }
    }
}
