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
        Assert.Equal(18, promotion.TotalUnits);
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
        Assert.Equal(9, promotion.TotalUnits);
    }

    [Fact]
    public async Task DomainSelector_NormalizesMunicipalityAndOverridesGeographicFalsePositive()
    {
        string html = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "source-formats",
            "exxacon-living-natura.html"));
        LayeredPromotionExtractor extractor = new();
        SourceDefinition source = new()
        {
            Id = "exxacon-living-natura",
            SourceKind = SourceKind.OfficialPromoter,
            StartUrls = ["https://www.exxacon.es/promocion/living-natura/"],
            AllowedHosts = ["www.exxacon.es"],
            MunicipalityHints = ["Galapagar"],
            CustomSelectors = new Dictionary<string, string>
            {
                ["name"] = "main h1",
                ["municipality"] = "#officeLink .elementor-icon-box-title",
                ["address"] = "#officeLink .elementor-icon-box-title"
            }
        };
        MunicipalityDefinition[] municipalities =
        [
            new() { OfficialName = "Guadarrama" },
            new() { OfficialName = "Galapagar" }
        ];

        Promotion promotion = Assert.Single(await extractor.ExtractAsync(
            new FetchedPage(
                new("https://www.exxacon.es/promocion/living-natura/"),
                html,
                "text/html",
                DateTimeOffset.UtcNow,
                "fixture"),
            source,
            municipalities,
            CancellationToken.None));

        Assert.Equal("Living Natura, viviendas de obra nueva", promotion.Name);
        Assert.Equal("Galapagar", promotion.Municipality);
        Assert.Equal("C. del Almendro, 1-A, 28260, Galapagar (Madrid)", promotion.Address);
        Assert.Equal(925_000m, promotion.PriceFrom);
        Assert.Equal(3, promotion.BedroomsMin);
        Assert.Equal(4, promotion.BedroomsMax);
        Assert.Equal(256m, promotion.BuiltAreaMinSqm);
        Assert.Equal(365m, promotion.BuiltAreaMaxSqm);
        Assert.Null(promotion.PlotAreaMinSqm);
        Assert.Equal(28, promotion.TotalUnits);
        Assert.Equal(1, promotion.AvailableUnits);
        Assert.Equal(CommercialStatus.LastUnits, promotion.CommercialStatus);
        Assert.Contains(
            promotion.Evidence,
            item => item.Field == "municipality" &&
                    item.Extractor == "DomainSpecificSelectorExtractor");
    }

    [Fact]
    public async Task ReviewedSourceScope_UsesOnlyPromotionContentAndFixedMunicipality()
    {
        const string html = """
            <html>
              <body>
                <nav>Promociones en Guadarrama desde 99.000 €</nav>
                <main id="promotion">
                  <h1>Residencial del Bosque</h1>
                  <p>Cuatro chalets pareados desde 545.000 €.</p>
                </main>
              </body>
            </html>
            """;
        LayeredPromotionExtractor extractor = new();
        SourceDefinition source = new()
        {
            Id = "reviewed",
            SourceKind = SourceKind.OfficialPromoter,
            StartUrls = ["https://promotora.example/residencial"],
            FixedMunicipality = "Miraflores de la Sierra",
            ContentSelector = "#promotion"
        };

        Promotion promotion = Assert.Single(await extractor.ExtractAsync(
            new FetchedPage(
                new("https://promotora.example/residencial"),
                html,
                "text/html",
                DateTimeOffset.UtcNow,
                "fixture"),
            source,
            [
                new MunicipalityDefinition { OfficialName = "Guadarrama" },
                new MunicipalityDefinition { OfficialName = "Miraflores de la Sierra" }
            ],
            CancellationToken.None));

        Assert.Equal("Miraflores de la Sierra", promotion.Municipality);
        Assert.Equal(545_000m, promotion.PriceFrom);
        Assert.Null(promotion.PriceTo);
        Assert.Contains(
            promotion.Evidence,
            item => item.Field == "municipality" &&
                    item.Extractor == "ReviewedSourceConfiguration");
    }

    [Fact]
    public async Task ReviewedSourceScope_FailsClosedWhenSelectorDisappears()
    {
        LayeredPromotionExtractor extractor = new();
        SourceDefinition source = new()
        {
            Id = "reviewed",
            SourceKind = SourceKind.OfficialPromoter,
            StartUrls = ["https://promotora.example/residencial"],
            FixedMunicipality = "Moralzarzal",
            ContentSelector = "body",
            AdditionalContentSelectors = ["#missing"]
        };

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => extractor.ExtractAsync(
                new FetchedPage(
                    new("https://promotora.example/residencial"),
                    "<html><body><h1>Residencial</h1></body></html>",
                    "text/html",
                    DateTimeOffset.UtcNow,
                    "fixture"),
                source,
                Municipalities,
                CancellationToken.None));

        Assert.Contains("#missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TextExtractor_PreservesBoundariesBetweenAdjacentElements()
    {
        const string html = """
            <html>
              <body>
                <main>
                  <div>1/17</div>
                  <p>18 viviendas unifamiliares en Moralzarzal.</p>
                  <h1>Residencial Ladera</h1>
                </main>
              </body>
            </html>
            """;
        LayeredPromotionExtractor extractor = new();
        SourceDefinition source = new()
        {
            Id = "element-boundaries",
            SourceKind = SourceKind.CooperativeManager,
            StartUrls = ["https://promotora.example/ladera"],
            ContentSelector = "main"
        };

        Promotion promotion = Assert.Single(await extractor.ExtractAsync(
            new FetchedPage(
                new("https://promotora.example/ladera"),
                html,
                "text/html",
                DateTimeOffset.UtcNow,
                "fixture"),
            source,
            Municipalities,
            CancellationToken.None));

        Assert.Equal(18, promotion.TotalUnits);
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
