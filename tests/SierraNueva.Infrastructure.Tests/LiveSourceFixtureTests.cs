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
            },
            {
                "inara-residencial-el-boalo",
                "inara-residencial-el-boalo.html"
            },
            {
                "doce-fresnos-moralzarzal",
                "doce-fresnos-moralzarzal.html"
            },
            {
                "galivivienda-villa-barbara",
                "galivivienda-villa-barbara.html"
            },
            {
                "galivivienda-montelago",
                "galivivienda-montelago.html"
            },
            {
                "grupo-insur-caliope",
                "grupo-insur-caliope.html"
            },
            {
                "soldit-nuevo-brunete",
                "soldit-nuevo-brunete.html"
            },
            {
                "gemg-cielo-arroyomolinos",
                "gemg-cielo-arroyomolinos.html"
            },
            {
                "grupo-egido-novaciempozuelos",
                "grupo-egido-novaciempozuelos.html"
            },
            {
                "grupo-impulsa-los-molinos",
                "grupo-impulsa-los-molinos.html"
            },
            {
                "grupo-impulsa-los-molinos-ii",
                "grupo-impulsa-los-molinos-ii.html"
            },
            {
                "bimaran-arroyo-homes",
                "bimaran-arroyo-homes.html"
            },
            {
                "antaro-mostoles-polar",
                "antaro-mostoles-polar.html"
            },
            {
                "trinosa-bonart",
                "trinosa-bonart.html"
            },
            {
                "kronos-krest",
                "kronos-krest.html"
            },
            {
                "kronos-mirage",
                "kronos-mirage.html"
            },
            {
                "cambrija-viridiana",
                "cambrija-viridiana.html"
            },
            {
                "larvin-hernan-cortes",
                "larvin-hernan-cortes.html"
            },
            {
                "larvin-rivas-zipi-zape",
                "larvin-rivas-zipi-zape.html"
            },
            {
                "larvin-monte-villa",
                "larvin-monte-villa.html"
            },
            {
                "larvin-residencial-48",
                "larvin-residencial-48.html"
            },
            {
                "brosh-villa-2-mostoles",
                "brosh-villa-2-mostoles.html"
            },
            {
                "brosh-villa-6-navalcarnero-f1",
                "brosh-villa-6-navalcarnero-f1.html"
            },
            {
                "brosh-villa-6-navalcarnero-f2",
                "brosh-villa-6-navalcarnero-f2.html"
            },
            {
                "naiz-aceris-pozuelo",
                "naiz-aceris-pozuelo.html"
            },
            {
                "naiz-pinea-pozuelo",
                "naiz-pinea-pozuelo.html"
            },
            {
                "miraflores-villas",
                "miraflores-villas.html"
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
        (SourceDefinition source, Promotion promotion) =
            await ExtractFixtureAsync(sourceId, fixtureName);

        Assert.Equal(source.FixedMunicipality, promotion.Municipality);
        Assert.False(string.IsNullOrWhiteSpace(promotion.Name));
        Assert.Contains(
            promotion.Evidence,
            item => item.Extractor == "ReviewedSourceConfiguration");
        AssertSourceSpecificFields(sourceId, promotion);
    }

    [Fact]
    public async Task SolditAlbor_ExtractsRenamedFormatWithoutOldListOrUnrelatedSectorTotals()
    {
        (_, Promotion promotion) = await ExtractFixtureAsync(
            "soldit-nuevo-brunete",
            "soldit-albor.html");

        Assert.Equal("Albor", promotion.Name);
        Assert.Equal("Brunete", promotion.Municipality);
        Assert.Equal(28, promotion.TotalUnits);
        Assert.Equal(4, promotion.BedroomsMin);
        Assert.Equal(5, promotion.BedroomsMax);
        Assert.Equal(566_000m, promotion.PriceFrom);
        Assert.Equal(["Adosado"], promotion.PropertyTypes);
        Assert.Null(promotion.HasPrivatePool);
        Assert.Equal("https://soldithomes.es/promociones/albor-brunete/", promotion.CanonicalUrl);
        Assert.Contains("https://soldithomes.es/promociones/nuevo-brunete/", promotion.SourceUrls);
        Assert.Contains(
            promotion.Evidence,
            item => item.Field == "canonicalUrlAlias" &&
                    item.SourceUrl == "https://soldithomes.es/promociones/nuevo-brunete/");
    }

    private static async Task<(SourceDefinition Source, Promotion Promotion)> ExtractFixtureAsync(
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

        return (source, promotion);
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
            case "inara-residencial-el-boalo":
                Assert.Equal("Residencial El Boalo", promotion.Name);
                Assert.Equal(5, promotion.TotalUnits);
                Assert.Equal(499_000m, promotion.PriceFrom);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(3, promotion.BathroomsMin);
                Assert.True(promotion.HasPrivatePool);
                Assert.Equal("Inara Smart Homes S.L.", promotion.DeveloperName);
                Assert.Empty(promotion.PropertyTypes);
                break;
            case "doce-fresnos-moralzarzal":
                Assert.Equal(12, promotion.TotalUnits);
                Assert.Equal(465_000m, promotion.PriceFrom);
                Assert.Null(promotion.PriceTo);
                Assert.Equal(180m, promotion.BuiltAreaMinSqm);
                Assert.Null(promotion.BuiltAreaMaxSqm);
                Assert.Equal(375m, promotion.PlotAreaMinSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(3, promotion.BathroomsMin);
                Assert.Null(promotion.GarageSpacesMin);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "galivivienda-villa-barbara":
                Assert.Equal(15, promotion.TotalUnits);
                Assert.Null(promotion.AvailableUnits);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(139m, promotion.BuiltAreaMinSqm);
                Assert.Equal(189m, promotion.PlotAreaMinSqm);
                Assert.Equal(404m, promotion.PlotAreaMaxSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.True(promotion.HasCommunityPool);
                Assert.Equal("Concedida", promotion.BuildingLicenceStatus);
                Assert.Equal(2, promotion.GarageSpacesMin);
                Assert.Contains("2028", promotion.DeliveryDateText);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "galivivienda-montelago":
                Assert.Null(promotion.TotalUnits);
                Assert.Equal(543_500m, promotion.PriceFrom);
                Assert.Equal(588_100m, promotion.PriceTo);
                Assert.Null(promotion.BuiltAreaMinSqm);
                Assert.Null(promotion.PlotAreaMinSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(4, promotion.BathroomsMin);
                Assert.Equal(2, promotion.GarageSpacesMin);
                Assert.True(promotion.HasCommunityPool);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(["Adosado"], promotion.PropertyTypes);
                break;
            case "grupo-insur-caliope":
                Assert.Equal("Insur Calíope", promotion.Name);
                Assert.Equal(44, promotion.TotalUnits);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(4, promotion.BedroomsMax);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(
                    ConstructionStatus.UnderConstruction,
                    promotion.ConstructionStatus);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(["Adosado"], promotion.PropertyTypes);
                break;
            case "soldit-nuevo-brunete":
                Assert.Equal("Nuevo Brunete", promotion.Name);
                Assert.Null(promotion.TotalUnits);
                Assert.Null(promotion.BedroomsMin);
                Assert.Equal(CommercialStatus.Upcoming, promotion.CommercialStatus);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "gemg-cielo-arroyomolinos":
                Assert.Equal(
                    "Chalets en Arroyomolinos – Residencial Cielo Arroyomolinos",
                    promotion.Name);
                Assert.Equal(20, promotion.TotalUnits);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Null(promotion.BuiltAreaMinSqm);
                Assert.Equal("Concedida", promotion.BuildingLicenceStatus);
                Assert.True(promotion.HasCommunityPool);
                Assert.Empty(promotion.PropertyTypes);
                break;
            case "grupo-egido-novaciempozuelos":
                Assert.Equal("NovaCiempozuelos | Grupo Egido", promotion.Name);
                Assert.Null(promotion.TotalUnits);
                Assert.Null(promotion.BedroomsMin);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(CommercialStatus.Upcoming, promotion.CommercialStatus);
                Assert.True(promotion.HasCommunityPool);
                Assert.Empty(promotion.PropertyTypes);
                break;
            case "grupo-impulsa-los-molinos":
                Assert.Equal("Los Molinos", promotion.Name);
                Assert.Equal(14, promotion.TotalUnits);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(4, promotion.BedroomsMax);
                Assert.Equal(205m, promotion.BuiltAreaMinSqm);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(2, promotion.GarageSpacesMin);
                Assert.Equal(["Adosado"], promotion.PropertyTypes);
                break;
            case "grupo-impulsa-los-molinos-ii":
                Assert.Equal("Los Molinos II", promotion.Name);
                Assert.Equal(12, promotion.TotalUnits);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(222m, promotion.BuiltAreaMinSqm);
                Assert.Equal(178m, promotion.PlotAreaMinSqm);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(2, promotion.GarageSpacesMin);
                Assert.Empty(promotion.PropertyTypes);
                break;
            case "bimaran-arroyo-homes":
                Assert.Equal("Arroyo Homes", promotion.Name);
                Assert.Equal(18, promotion.TotalUnits);
                Assert.Null(promotion.BuiltAreaMinSqm);
                Assert.Null(promotion.BedroomsMin);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(
                    ConstructionStatus.UnderConstruction,
                    promotion.ConstructionStatus);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "antaro-mostoles-polar":
                Assert.Equal("Promoción Móstoles Polar Residencial", promotion.Name);
                Assert.Equal(11, promotion.TotalUnits);
                Assert.Equal(481_000m, promotion.PriceFrom);
                Assert.Null(promotion.PriceTo);
                Assert.Empty(promotion.PropertyTypes);
                break;
            case "trinosa-bonart":
                Assert.Equal("Bonart", promotion.Name);
                Assert.Equal(8, promotion.TotalUnits);
                Assert.Equal(3_200_000m, promotion.PriceFrom);
                Assert.Equal(1_000m, promotion.BuiltAreaMinSqm);
                Assert.Equal(1_500m, promotion.PlotAreaMinSqm);
                Assert.Null(promotion.PlotAreaMaxSqm);
                Assert.True(promotion.HasPrivatePool);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(
                    ConstructionStatus.UnderConstruction,
                    promotion.ConstructionStatus);
                break;
            case "kronos-krest":
                Assert.Equal("Krest", promotion.Name);
                Assert.Equal(31, promotion.TotalUnits);
                Assert.Equal(1_080_000m, promotion.PriceFrom);
                Assert.Equal(1_130_000m, promotion.PriceTo);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(5, promotion.BedroomsMax);
                Assert.Equal(CommercialStatus.LastUnits, promotion.CommercialStatus);
                Assert.Equal(["Pareado"], promotion.PropertyTypes);
                break;
            case "kronos-mirage":
                Assert.Equal("Mirage", promotion.Name);
                Assert.Equal(790_800m, promotion.PriceFrom);
                Assert.Equal(920_800m, promotion.PriceTo);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Null(promotion.BedroomsMax);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                break;
            case "cambrija-viridiana":
                Assert.Equal("VIRIDIANA", promotion.Name);
                Assert.Equal(1_232_000m, promotion.PriceFrom);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(5, promotion.BedroomsMax);
                Assert.True(promotion.HasCommunityPool);
                Assert.Equal(["Adosado", "Pareado"], promotion.PropertyTypes);
                break;
            case "larvin-hernan-cortes":
                Assert.Equal("Chalets en Boadilla del Monte", promotion.Name);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(5, promotion.BedroomsMax);
                Assert.True(promotion.HasCommunityPool);
                break;
            case "larvin-rivas-zipi-zape":
                Assert.Equal("Chalets en Rivas", promotion.Name);
                Assert.Equal(14, promotion.TotalUnits);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(4, promotion.BedroomsMax);
                Assert.Null(promotion.PriceFrom);
                break;
            case "larvin-monte-villa":
                Assert.Equal("Chalets en Villaviciosa de Odón", promotion.Name);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(525m, promotion.PlotAreaMinSqm);
                Assert.Contains("Independiente", promotion.PropertyTypes);
                break;
            case "larvin-residencial-48":
                Assert.Equal("Viviendas en Villaviciosa de Odón", promotion.Name);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.True(promotion.HasCommunityPool);
                Assert.Contains("Independiente", promotion.PropertyTypes);
                Assert.Null(promotion.TotalUnits);
                break;
            case "brosh-villa-2-mostoles":
                Assert.Equal("Villa 2 Móstoles", promotion.Name);
                Assert.Equal(17, promotion.TotalUnits);
                Assert.Equal(580_000m, promotion.PriceFrom);
                Assert.Equal(620_000m, promotion.PriceTo);
                Assert.Equal(200.43m, promotion.BuiltAreaMinSqm);
                Assert.Equal(250m, promotion.BuiltAreaMaxSqm);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(3, promotion.BathroomsMin);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(
                    ConstructionStatus.UnderConstruction,
                    promotion.ConstructionStatus);
                Assert.Equal(["Adosado"], promotion.PropertyTypes);
                break;
            case "brosh-villa-6-navalcarnero-f1":
                Assert.Equal("Villa 6 Navalcarnero | Fase 1", promotion.Name);
                Assert.Equal(22, promotion.TotalUnits);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(262.32m, promotion.BuiltAreaMinSqm);
                Assert.Equal(274.41m, promotion.BuiltAreaMaxSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(3, promotion.BathroomsMin);
                Assert.Equal(
                    ConstructionStatus.UnderConstruction,
                    promotion.ConstructionStatus);
                break;
            case "brosh-villa-6-navalcarnero-f2":
                Assert.Equal("Villa 6 Navalcarnero | Fase 2", promotion.Name);
                Assert.Equal(26, promotion.TotalUnits);
                Assert.Equal(572_000m, promotion.PriceFrom);
                Assert.Equal(649_000m, promotion.PriceTo);
                Assert.Equal(268.49m, promotion.BuiltAreaMinSqm);
                Assert.Equal(273.80m, promotion.BuiltAreaMaxSqm);
                Assert.Equal(4, promotion.BedroomsMin);
                Assert.Equal(3, promotion.BathroomsMin);
                Assert.Equal("Concedida", promotion.BuildingLicenceStatus);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(
                    ConstructionStatus.UnderConstruction,
                    promotion.ConstructionStatus);
                Assert.Equal(["Adosado"], promotion.PropertyTypes);
                break;
            case "naiz-aceris-pozuelo":
                Assert.Equal("Aceris de Pozuelo | Naiz Homes", promotion.Name);
                Assert.Null(promotion.TotalUnits);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(CommercialStatus.Upcoming, promotion.CommercialStatus);
                Assert.Empty(promotion.PropertyTypes);
                break;
            case "naiz-pinea-pozuelo":
                Assert.Equal("Pinea de Pozuelo | Naiz Homes", promotion.Name);
                Assert.Equal(11, promotion.TotalUnits);
                Assert.Null(promotion.PriceFrom);
                Assert.Equal(5, promotion.BedroomsMin);
                Assert.Equal(6, promotion.BedroomsMax);
                Assert.Equal(500m, promotion.PlotAreaMinSqm);
                Assert.Equal(700m, promotion.PlotAreaMaxSqm);
                Assert.True(promotion.HasPrivatePool);
                Assert.Equal(CommercialStatus.OnSale, promotion.CommercialStatus);
                Assert.Equal(["Independiente"], promotion.PropertyTypes);
                break;
            case "miraflores-villas":
                Assert.Null(promotion.TotalUnits);
                Assert.Null(promotion.PriceFrom);
                Assert.Null(promotion.BuiltAreaMinSqm);
                Assert.Equal(1000m, promotion.PlotAreaMinSqm);
                Assert.Equal(3, promotion.BedroomsMin);
                Assert.Equal(4, promotion.BedroomsMax);
                Assert.Empty(promotion.PropertyTypes);
                Assert.Null(promotion.HasPrivatePool);
                break;
            default:
                throw new InvalidOperationException($"Fuente no cubierta: {sourceId}");
        }
    }
}
