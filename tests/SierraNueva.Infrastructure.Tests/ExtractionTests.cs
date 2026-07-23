using SierraNueva.Contracts;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Extraction;

namespace SierraNueva.Infrastructure.Tests;

public sealed class ExtractionTests
{
    private static readonly MunicipalityDefinition[] Municipalities =
    [
        new()
        {
            OfficialName = "Moralzarzal",
            Latitude = 40.675,
            Longitude = -3.969444
        },
        new()
        {
            OfficialName = "Soto del Real",
            Aliases = ["Soto"],
            Latitude = 40.75416,
            Longitude = -3.78333
        }
    ];

    [Fact]
    public async Task JsonLdExtractor_PrefersStructuredData()
    {
        string html = await ReadFixtureAsync("01-jsonld-promotion.html");
        LayeredPromotionExtractor extractor = new();

        IReadOnlyList<Promotion> result = await extractor.ExtractAsync(
            new FetchedPage(
                new("https://fixtures.sierranueva.test/residencial-cumbre"),
                html,
                "text/html",
                DateTimeOffset.UtcNow,
                "fixture"),
            CreateSource(),
            Municipalities,
            CancellationToken.None);

        Promotion promotion = Assert.Single(result);
        Assert.Equal("Residencial Cumbre", promotion.Name);
        Assert.Equal(475_000m, promotion.PriceFrom);
        Assert.Equal(535_000m, promotion.PriceTo);
        Assert.Equal(165m, promotion.BuiltAreaMinSqm);
        Assert.Equal(280m, promotion.PlotAreaMinSqm);
        Assert.Equal(LocationPrecision.ExactCoordinates, promotion.LocationPrecision);
        Assert.Contains(promotion.Evidence, item => item.Extractor == "JsonLdPromotionExtractor");
    }

    [Fact]
    public async Task TextExtractor_RecognizesSpanishFormats()
    {
        string html = await ReadFixtureAsync("02-text-promotion.html");
        LayeredPromotionExtractor extractor = new();

        IReadOnlyList<Promotion> result = await extractor.ExtractAsync(
            new FetchedPage(
                new("https://fixtures.sierranueva.test/encinar-soto"),
                html,
                "text/html",
                DateTimeOffset.UtcNow,
                "fixture"),
            CreateSource(),
            Municipalities,
            CancellationToken.None);

        Promotion promotion = Assert.Single(result);
        Assert.Equal(625_000m, promotion.PriceFrom);
        Assert.Equal(210m, promotion.BuiltAreaMinSqm);
        Assert.Equal(510m, promotion.PlotAreaMinSqm);
        Assert.Equal(4, promotion.BedroomsMin);
        Assert.Contains("Independiente", promotion.PropertyTypes);
        Assert.True(promotion.HasPrivatePool);
        Assert.Equal(ConstructionStatus.UnderConstruction, promotion.ConstructionStatus);
    }

    [Fact]
    public async Task TextExtractor_RecognizesSoldOutPromotion()
    {
        string html = await ReadFixtureAsync("03-sold-out-promotion.html");
        LayeredPromotionExtractor extractor = new();

        Promotion promotion = Assert.Single(await extractor.ExtractAsync(
            new FetchedPage(
                new("https://fixtures.sierranueva.test/puerta-pedriza"),
                html,
                "text/html",
                DateTimeOffset.UtcNow,
                "fixture"),
            CreateSource(),
            [
                new MunicipalityDefinition { OfficialName = "Manzanares el Real" }
            ],
            CancellationToken.None));

        Assert.Equal(CommercialStatus.SoldOut, promotion.CommercialStatus);
        Assert.Equal(ConstructionStatus.Completed, promotion.ConstructionStatus);
    }

    private static SourceDefinition CreateSource()
    {
        return new()
        {
            Id = "fixture",
            SourceKind = SourceKind.OfficialPromoter,
            StartUrls = ["https://fixtures.sierranueva.test/promociones"]
        };
    }

    private static Task<string> ReadFixtureAsync(string filename)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "test-data", "html", filename);
        return File.ReadAllTextAsync(path);
    }
}
