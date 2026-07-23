using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Crawling;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Extraction;
using SierraNueva.Infrastructure.Geocoding;
using SierraNueva.Infrastructure.Persistence;

namespace SierraNueva.Infrastructure.Tests;

public sealed class PipelineIntegrationTests
{
    [Fact]
    public async Task Pipeline_PreservesThenDeactivatesMissingPromotionAfterThreeRuns()
    {
        string root = CreateTempDirectory();
        try
        {
            MutablePageSource pages = new();
            FakeClock clock = new(new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero));
            CrawlPipeline pipeline = new(
                pages,
                new LayeredPromotionExtractor(),
                new MunicipalityCentroidGeocoder(),
                new JsonPromotionStateRepository(),
                new PublicDataWriter(),
                clock);
            CrawlRequest request = CreateRequest(root);
            pages.Pages =
            [
                new FetchedPage(
                    new("https://example.com/cumbre"),
                    """
                    <html><head><meta property="og:title" content="Residencial Cumbre"></head>
                    <body><h1>Residencial Cumbre</h1>
                    <p>Promoción de chalets pareados en Moralzarzal. Desde 475.000 €.</p>
                    </body></html>
                    """,
                    "text/html",
                    clock.UtcNow,
                    "fixture")
            ];

            CrawlResult first = await pipeline.RunAsync(request, CancellationToken.None);
            Assert.True(Assert.Single(first.Dataset.Promotions).Active);

            pages.Pages = [];
            for (int run = 0; run < 3; run++)
            {
                clock.Advance(TimeSpan.FromDays(1));
                await pipeline.RunAsync(request, CancellationToken.None);
            }

            IReadOnlyList<Promotion> state = await new JsonPromotionStateRepository().LoadAsync(
                Path.Combine(root, "state"),
                CancellationToken.None);
            Promotion promotion = Assert.Single(state);
            Assert.False(promotion.Active);
            Assert.Equal(3, promotion.ConsecutiveMisses);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CrawlRequest CreateRequest(string root)
    {
        return new()
        {
            Sources =
            [
                new SourceDefinition
                {
                    Id = "fixture",
                    Name = "Fixture",
                    BaseUrl = "https://example.com",
                    Enabled = true,
                    SourceKind = SourceKind.OfficialPromoter,
                    StartUrls = ["https://example.com/cumbre"]
                }
            ],
            Municipalities =
            [
                new MunicipalityDefinition
                {
                    OfficialName = "Moralzarzal",
                    Latitude = 40.675,
                    Longitude = -3.969
                }
            ],
            Settings = new()
            {
                DeactivateAfterMisses = 3,
                PublicChangeLimit = 100
            },
            OutputDirectory = Path.Combine(root, "public"),
            StateDirectory = Path.Combine(root, "state")
        };
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sierranueva-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class MutablePageSource : IPageSource
    {
        public IReadOnlyList<FetchedPage> Pages { get; set; } = [];

        public Task<PageBatch> FetchAsync(
            SourceDefinition source,
            CrawlerSettings settings,
            int? maxPagesOverride,
            bool disablePlaywright,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PageBatch
            {
                Pages = Pages,
                DiscoveredUrls = Pages.Count
            });
        }
    }

    private sealed class FakeClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = value;

        public void Advance(TimeSpan valueToAdd)
        {
            UtcNow += valueToAdd;
        }
    }
}
