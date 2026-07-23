using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Crawling;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;
using SierraNueva.Infrastructure.Crawling;
using SierraNueva.Infrastructure.Discovery;
using SierraNueva.Infrastructure.Documents;
using SierraNueva.Infrastructure.Extraction;
using SierraNueva.Infrastructure.Geocoding;
using SierraNueva.Infrastructure.Persistence;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Tests;

public sealed class PipelineIntegrationTests
{
    [Fact]
    public async Task Pipeline_FetchesFixtureThroughRealLoopbackHttpAndPublishesOutputs()
    {
        string root = CreateTempDirectory();
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "html",
            "01-jsonld-promotion.html");
        await using LoopbackFixtureServer server = await LoopbackFixtureServer.StartAsync(fixturePath);
        try
        {
            string stateDirectory = Path.Combine(root, "state");
            using HttpClient httpClient = new(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false
            })
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            using FileHttpMetadataCache metadataCache = new(stateDirectory);
            RespectfulPageSource pages = new(
                new SingleHttpClientFactory(httpClient),
                [new ConfiguredUrlDiscoveryProvider()],
                new LoopbackOnlyUrlPolicy(server.PromotionUri),
                new InternalLinkDiscoveryProvider(),
                new NullDynamicPageRenderer(),
                new PdfPigTextExtractor(),
                metadataCache,
                NullLogger<RespectfulPageSource>.Instance);
            FakeClock clock = new(new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero));
            CrawlPipeline pipeline = new(
                pages,
                new LayeredPromotionExtractor(),
                new MunicipalityCentroidGeocoder(),
                new JsonPromotionStateRepository(),
                new PublicDataWriter(),
                clock);
            string outputDirectory = Path.Combine(root, "public");
            CrawlRequest request = CreateRequest(
                root,
                new SourceDefinition
                {
                    Id = "http-local",
                    Name = "Servidor HTTP local de fixtures",
                    BaseUrl = server.Origin.AbsoluteUri,
                    Enabled = true,
                    SourceKind = SourceKind.OfficialPromoter,
                    AllowedHosts = [server.PromotionUri.Host],
                    StartUrls = [server.PromotionUri.AbsoluteUri],
                    UseRobots = false,
                    UseSitemaps = false,
                    FollowInternalLinks = false,
                    MaxPages = 1,
                    RequestDelayMilliseconds = 0,
                    UsePlaywright = false
                });

            CrawlResult result = await pipeline.RunAsync(request, CancellationToken.None);

            Assert.True(result.HasPublishableData);
            Assert.Equal(RunStatus.Success, result.Run.Status);
            Assert.Equal(1, result.Run.DiscoveredUrls);
            Assert.Equal(1, result.Run.FetchedUrls);
            Promotion promotion = Assert.Single(result.Dataset.Promotions);
            Assert.Equal("Residencial Cumbre", promotion.Name);
            Assert.Equal("Moralzarzal", promotion.Municipality);
            Assert.Equal(475_000m, promotion.PriceFrom);
            Assert.Equal(
                UrlNormalizer.Normalize(server.PromotionUri.AbsoluteUri),
                promotion.CanonicalUrl);
            Assert.Equal(LocationPrecision.ExactCoordinates, promotion.LocationPrecision);

            Assert.Equal("GET /residencial-cumbre HTTP/1.1", server.RequestLine);
            Assert.Contains("SierraNueva.Tests/1.0", server.UserAgent, StringComparison.Ordinal);

            string[] publicFiles =
            [
                "changes.json",
                "promotions.csv",
                "promotions.geojson",
                "promotions.json",
                "run.json"
            ];
            Assert.All(publicFiles, filename =>
                Assert.True(File.Exists(Path.Combine(outputDirectory, filename)), filename));
            Assert.False(Directory.Exists(Path.Combine(outputDirectory, "state")));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories));

            PromotionDataset? persistedDataset = JsonSerializer.Deserialize<PromotionDataset>(
                await File.ReadAllTextAsync(Path.Combine(outputDirectory, "promotions.json")),
                JsonDefaults.Compact);
            Assert.Equal(result.Run.RunId, Assert.IsType<PromotionDataset>(persistedDataset).RunId);

            IReadOnlyList<Promotion> persistedState =
                await new JsonPromotionStateRepository().LoadAsync(
                    stateDirectory,
                    CancellationToken.None);
            Assert.Equal(promotion.Id, Assert.Single(persistedState).Id);

            string metadataJson = await File.ReadAllTextAsync(
                Path.Combine(stateDirectory, "http-cache.json"));
            Assert.Contains(server.PromotionUri.AbsoluteUri, metadataJson, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Pipeline_TreatsExhaustedRequestTimeoutAsPartialSourceFailure()
    {
        string root = CreateTempDirectory();
        try
        {
            using HttpClient httpClient = new(new TimeoutRoutingHandler())
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            using FileHttpMetadataCache metadataCache = new(Path.Combine(root, "state"));
            RespectfulPageSource pages = new(
                new SingleHttpClientFactory(httpClient),
                [new ConfiguredUrlDiscoveryProvider()],
                new AllowAllUrlPolicy(),
                new InternalLinkDiscoveryProvider(),
                new NullDynamicPageRenderer(),
                new PdfPigTextExtractor(),
                metadataCache,
                NullLogger<RespectfulPageSource>.Instance);
            CrawlPipeline pipeline = new(
                pages,
                new LayeredPromotionExtractor(),
                new MunicipalityCentroidGeocoder(),
                new JsonPromotionStateRepository(),
                new PublicDataWriter(),
                new FakeClock(new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero)));
            CrawlRequest request = new()
            {
                Sources =
                [
                    CreateHttpSource("a-timeout", "https://sources.example/slow"),
                    CreateHttpSource("b-valid", "https://sources.example/valid")
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
                    UserAgent = "SierraNueva.Tests/1.0",
                    RequestDelayMilliseconds = 0,
                    TimeoutSeconds = 1,
                    MaxRetries = 0,
                    DeactivateAfterMisses = 3,
                    PublicChangeLimit = 100
                },
                OutputDirectory = Path.Combine(root, "public"),
                StateDirectory = Path.Combine(root, "state")
            };

            CrawlResult result = await pipeline.RunAsync(request, CancellationToken.None);

            Assert.True(result.HasPublishableData);
            Assert.Equal(RunStatus.PartialSuccess, result.Run.Status);
            Assert.Equal(SourceRunStatus.Failed, result.Run.SourceResults[0].Status);
            Assert.Contains(
                "Tiempo de espera agotado",
                Assert.Single(result.Run.SourceResults[0].Errors),
                StringComparison.Ordinal);
            Assert.Equal(SourceRunStatus.Success, result.Run.SourceResults[1].Status);
            Assert.Equal("Residencial Resiliente", Assert.Single(result.Dataset.Promotions).Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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

    [Fact]
    public async Task Pipeline_GeocodesPreviousStateWhenSourceReturnsNoPage()
    {
        string root = CreateTempDirectory();
        try
        {
            string stateDirectory = Path.Combine(root, "state");
            Promotion previous = new()
            {
                Id = "sn-existing",
                Name = "Residencial Cumbre",
                Municipality = "Moralzarzal",
                CanonicalUrl = "https://example.com/cumbre",
                SourceKind = SourceKind.OfficialPromoter,
                SourceConfidence = 0.9m,
                FirstSeenUtc = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero),
                LastSeenUtc = new DateTimeOffset(2026, 7, 22, 8, 0, 0, TimeSpan.Zero),
                LastChangedUtc = new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero),
                Active = true
            };
            await new JsonPromotionStateRepository().SaveAsync(
                stateDirectory,
                [previous],
                CancellationToken.None);

            CrawlPipeline pipeline = new(
                new MutablePageSource(),
                new LayeredPromotionExtractor(),
                new MunicipalityCentroidGeocoder(),
                new JsonPromotionStateRepository(),
                new PublicDataWriter(),
                new FakeClock(new DateTimeOffset(2026, 7, 23, 8, 0, 0, TimeSpan.Zero)));

            CrawlResult result = await pipeline.RunAsync(
                CreateRequest(root),
                CancellationToken.None);

            Promotion located = Assert.Single(result.Dataset.Promotions);
            Assert.Equal(40.675, located.Latitude);
            Assert.Equal(-3.969, located.Longitude);
            Assert.Equal(LocationPrecision.MunicipalityCentroid, located.LocationPrecision);
            Assert.Equal(1, result.Dataset.Statistics.WithCoordinates);

            using JsonDocument geoJson = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, "public", "promotions.geojson")));
            Assert.Single(geoJson.RootElement.GetProperty("features").EnumerateArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CrawlRequest CreateRequest(
        string root,
        SourceDefinition? source = null)
    {
        return new()
        {
            Sources =
            [
                source ?? new SourceDefinition
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
                UserAgent = "SierraNueva.Tests/1.0",
                RequestDelayMilliseconds = 0,
                MaxRetries = 0,
                DeactivateAfterMisses = 3,
                PublicChangeLimit = 100
            },
            OutputDirectory = Path.Combine(root, "public"),
            StateDirectory = Path.Combine(root, "state")
        };
    }

    private static SourceDefinition CreateHttpSource(string id, string url)
    {
        return new()
        {
            Id = id,
            Name = id,
            BaseUrl = "https://sources.example/",
            Enabled = true,
            SourceKind = SourceKind.OfficialPromoter,
            AllowedHosts = ["sources.example"],
            StartUrls = [url],
            UseRobots = false,
            UseSitemaps = false,
            FollowInternalLinks = false,
            MaxPages = 1,
            RequestDelayMilliseconds = 0,
            FixedMunicipality = "Moralzarzal",
            ContentSelector = "main",
            CustomSelectors = new Dictionary<string, string>
            {
                ["name"] = "main h1"
            }
        };
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sierranueva-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class SingleHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class LoopbackOnlyUrlPolicy(Uri allowedUri) : IUrlPolicy
    {
        public bool IsAllowed(Uri url, SourceDefinition source, out SkipReason reason)
        {
            bool isAllowed = url.Scheme == Uri.UriSchemeHttp &&
                             url.Host == allowedUri.Host &&
                             url.Port == allowedUri.Port;
            reason = isAllowed ? SkipReason.None : SkipReason.PrivateNetwork;
            return isAllowed;
        }
    }

    private sealed class AllowAllUrlPolicy : IUrlPolicy
    {
        public bool IsAllowed(Uri url, SourceDefinition source, out SkipReason reason)
        {
            reason = SkipReason.None;
            return true;
        }
    }

    private sealed class TimeoutRoutingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/slow")
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("El timeout debe cancelar la espera.");
            }

            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    <!doctype html>
                    <html lang="es">
                    <body>
                      <main>
                        <h1>Residencial Resiliente</h1>
                        <p>Promoción de 3 chalets pareados en Moralzarzal.</p>
                      </main>
                    </body>
                    </html>
                    """,
                    Encoding.UTF8,
                    "text/html")
            };
        }
    }

    private sealed class NullDynamicPageRenderer : IDynamicPageRenderer
    {
        public Task<string?> RenderAsync(Uri url, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Playwright no debe ejecutarse en esta prueba.");
        }
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

    private sealed class LoopbackFixtureServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _serveTask;
        private readonly byte[] _responseBody;

        private LoopbackFixtureServer(string html)
        {
            _listener = new(IPAddress.Loopback, 0);
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Origin = new($"http://127.0.0.1:{port}/");
            PromotionUri = new(Origin, "residencial-cumbre");
            _responseBody = Encoding.UTF8.GetBytes(
                html.Replace(
                    "https://fixtures.sierranueva.test/residencial-cumbre",
                    PromotionUri.AbsoluteUri,
                    StringComparison.Ordinal));
            _serveTask = ServeOnceAsync(_shutdown.Token);
        }

        public Uri Origin { get; }

        public Uri PromotionUri { get; }

        public string? RequestLine { get; private set; }

        public string? UserAgent { get; private set; }

        public static async Task<LoopbackFixtureServer> StartAsync(string fixturePath)
        {
            string html = await File.ReadAllTextAsync(fixturePath);
            return new(html);
        }

        public async ValueTask DisposeAsync()
        {
            await _shutdown.CancelAsync();
            _listener.Stop();
            try
            {
                await _serveTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException exception) when (
                exception.SocketErrorCode == SocketError.OperationAborted)
            {
            }

            _shutdown.Dispose();
        }

        private async Task ServeOnceAsync(CancellationToken cancellationToken)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(
                stream,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            RequestLine = await reader.ReadLineAsync(cancellationToken);
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } header)
            {
                if (header.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase))
                {
                    UserAgent = header["User-Agent:".Length..].Trim();
                }
            }

            string headers =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {_responseBody.Length}\r\n" +
                "ETag: \"fixture-v1\"\r\n" +
                "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), cancellationToken);
            await stream.WriteAsync(_responseBody, cancellationToken);
        }
    }
}
