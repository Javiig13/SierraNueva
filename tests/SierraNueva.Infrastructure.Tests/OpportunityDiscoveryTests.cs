using System.IO.Compression;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Discovery;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Configuration;
using SierraNueva.Infrastructure.Discovery;
using SierraNueva.Infrastructure.Persistence;

namespace SierraNueva.Infrastructure.Tests;

public sealed class OpportunityDiscoveryTests
{
    private static readonly DateOnly FixtureDate = new(2026, 5, 21);

    [Theory]
    [InlineData("bocm-rss.xml", OpportunityFeedFormat.Rss, 2)]
    [InlineData("boe-sumario.json", OpportunityFeedFormat.BoeJson, 2)]
    [InlineData("pcsp-licitaciones.atom", OpportunityFeedFormat.Atom, 3)]
    [InlineData("portal-suelo.html", OpportunityFeedFormat.Html, 2)]
    [InlineData("bocm-sumario.xml", OpportunityFeedFormat.BocmCalendar, 2)]
    [InlineData("eadmin-tablon.html", OpportunityFeedFormat.EAdminHtml, 2)]
    [InlineData("cercedilla-noticias.rss.xml", OpportunityFeedFormat.Rss, 2)]
    public async Task Parser_ReadsEachOfficialFormatFixture(
        string fixture,
        OpportunityFeedFormat format,
        int expected)
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "test",
            Name = "Test",
            Enabled = true,
            Format = format,
            ItemSelectors = [".component-carousel-item", ".accordion-content li"],
            MaxItems = 100
        };
        byte[] content = await File.ReadAllBytesAsync(FixturePath(fixture));

        IReadOnlyList<OpportunityFeedItem> items = new OpportunityFeedParser().Parse(
            source,
            content,
            new("https://official.example/feed"),
            FixtureDate);

        Assert.Equal(expected, items.Count);
        Assert.All(items, item => Assert.StartsWith("https://", item.OfficialUrl));
    }

    [Fact]
    public async Task Reader_FollowsBocmCalendarToTheOfficialXmlSummary()
    {
        byte[] summary = await File.ReadAllBytesAsync(FixturePath("bocm-sumario.xml"));
        using RecordingHttpClientFactory factory = new(summary);
        OpportunityFeedReader reader = new(factory, new OpportunityFeedParser());
        OpportunitySourceDefinition source = new()
        {
            Id = "bocm-calendar",
            Name = "BOCM",
            Enabled = true,
            SourceKind = OpportunitySourceKind.RegionalGazette,
            Format = OpportunityFeedFormat.BocmCalendar,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate =
                "https://www.bocm.es/search-day-month?field_date%5Bdate%5D=" +
                "{date:dd%2FMM%2Fyyyy}",
            AllowedHosts = ["www.bocm.es"],
            MaxItems = 100
        };

        IReadOnlyList<OpportunityFeedItem> items = await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal(2, factory.Requests.Count);
        Assert.Contains(
            "21%2F05%2F2026",
            factory.Requests[0].Query,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            "BOCM-20260521120.xml",
            factory.Requests[1].AbsolutePath,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_RejectsAnHtmlWafResponseForAZipFeed()
    {
        using StaticHttpClientFactory factory = new(
            """
            <html><title>Error</title><body>Request denied by WAF</body></html>
            """,
            "text/html");
        OpportunityFeedReader reader = new(factory, new OpportunityFeedParser());
        OpportunitySourceDefinition source = new()
        {
            Id = "pcsp",
            Name = "PCSP",
            Enabled = true,
            Format = OpportunityFeedFormat.ZipAtom,
            Cadence = OpportunityFeedCadence.Monthly,
            UrlTemplate = "https://official.example/archive-{date:yyyyMM}.zip",
            AllowedHosts = ["official.example"],
            MaxItems = 100
        };

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => reader.ReadAsync(
                source,
                FixtureDate,
                FixtureDate,
                CancellationToken.None));

        Assert.Contains("debía ser ZIP", exception.Message, StringComparison.Ordinal);
        Assert.Contains("text/html", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parser_ReadsPcspAtomInsideZip()
    {
        byte[] atom = await File.ReadAllBytesAsync(FixturePath("pcsp-licitaciones.atom"));
        using MemoryStream zip = new();
        using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("licitaciones.atom");
            await using Stream output = entry.Open();
            await output.WriteAsync(atom);
        }

        OpportunitySourceDefinition source = new()
        {
            Id = "pcsp",
            Name = "PCSP",
            Enabled = true,
            Format = OpportunityFeedFormat.ZipAtom,
            MaxItems = 100
        };

        IReadOnlyList<OpportunityFeedItem> items = new OpportunityFeedParser().Parse(
            source,
            zip.ToArray(),
            new("https://contrataciondelsectorpublico.gob.es/month.zip"),
            FixtureDate);

        Assert.Equal(3, items.Count);
        Assert.Contains(items, item => item.Title.Contains(
            "Miraflores de la Sierra",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parser_ReadsOnlyTheAllowedMunicipalHomepageNoticeTable()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "municipal-home",
            Name = "Sede municipal",
            Enabled = true,
            SourceKind = OpportunitySourceKind.MunicipalNoticeBoard,
            Format = OpportunityFeedFormat.Html,
            ItemSelectors = [".AdvertisementBoardHomeListPanel tr"],
            FixedMunicipality = "Navacerrada",
            MaxItems = 10
        };
        byte[] content = await File.ReadAllBytesAsync(
            FixturePath("sede-electronica-home.html"));

        IReadOnlyList<OpportunityFeedItem> items = new OpportunityFeedParser().Parse(
            source,
            content,
            new("https://aytonavacerrada.sedelectronica.es/"),
            FixtureDate);

        Assert.Equal(2, items.Count);
        Assert.All(
            items,
            item => Assert.StartsWith(
                "https://aytonavacerrada.sedelectronica.es/preview-document/",
                item.OfficialUrl,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parser_ReadsOnlyBustarviejoNoticeBoardContent()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "tablon-bustarviejo",
            Name = "Tablón de Bustarviejo",
            Enabled = true,
            SourceKind = OpportunitySourceKind.MunicipalNoticeBoard,
            Format = OpportunityFeedFormat.Html,
            ItemSelectors =
                ["#ContentBody_divContenidoEstructura .enlaceAppWeb li"],
            FixedMunicipality = "Bustarviejo",
            MaxItems = 100
        };
        byte[] content = await File.ReadAllBytesAsync(
            FixturePath("bustarviejo-tablon.html"));

        IReadOnlyList<OpportunityFeedItem> items = new OpportunityFeedParser().Parse(
            source,
            content,
            new(
                "https://transparenciabustarviejo.eadministracion.es/" +
                "transparencia/tablon-de-anuncios"),
            FixtureDate);

        Assert.Equal(2, items.Count);
        Assert.All(
            items,
            item => Assert.StartsWith(
                "https://transparenciabustarviejo.eadministracion.es/" +
                "transparencia/tablon-de-anuncios/",
                item.OfficialUrl,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pipeline_FiltersNoisePersistsPrivatelyAndPreservesReview()
    {
        string directory = CreateTempDirectory();
        try
        {
            ConfigurationLoader loader = new();
            OpportunityDiscoveryCatalog catalog =
                await loader.LoadOpportunityCatalogAsync(
                    ConfigPath("discovery-sources.json"),
                    CancellationToken.None);
            IReadOnlyList<MunicipalityDefinition> municipalities =
                await loader.LoadMunicipalitiesAsync(
                    ConfigPath("municipalities.json"),
                    CancellationToken.None);
            JsonOpportunityStateRepository repository = new();
            OpportunityDiscoveryPipeline pipeline = new(
                new OpportunityFeedReader(
                    new UnusedHttpClientFactory(),
                    new OpportunityFeedParser()),
                repository,
                new FixedClock(new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero)));
            OpportunityDiscoveryRequest request = new()
            {
                Catalog = catalog,
                Municipalities = municipalities,
                StateDirectory = directory,
                From = FixtureDate,
                To = FixtureDate
            };

            OpportunityDiscoveryResult first = await pipeline.RunAsync(
                request,
                CancellationToken.None);

            Assert.Equal(29, first.Run.NewCandidates);
            Assert.Equal(
                [
                    "Alpedrete",
                    "Becerril de la Sierra",
                    "Bustarviejo",
                    "Cabanillas de la Sierra",
                    "Cercedilla",
                    "Collado Mediano",
                    "Collado Villalba",
                    "El Boalo",
                    "El Escorial",
                    "Fresnedillas de la Oliva",
                    "Galapagar",
                    "Guadarrama",
                    "Hoyo de Manzanares",
                    "La Cabrera",
                    "Los Molinos",
                    "Manzanares el Real",
                    "Miraflores de la Sierra",
                    "Moralzarzal",
                    "Navacerrada",
                    "Navalagamella",
                    "San Lorenzo de El Escorial",
                    "Santa María de la Alameda",
                    "Soto del Real",
                    "Torrelodones",
                    "Valdemaqueda",
                    "Zarzalejo"
                ],
                first.State.Candidates
                    .Select(candidate => candidate.Municipality)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            Assert.True(File.Exists(Path.Combine(directory, "opportunity-candidates.json")));

            OpportunityCandidate reviewed = first.State.Candidates[0];
            OpportunityRadarState reviewedState = new()
            {
                UpdatedAtUtc = first.State.UpdatedAtUtc,
                LastRun = first.Run,
                Candidates = first.State.Candidates.Select(candidate =>
                    candidate.Id == reviewed.Id
                        ? CopyWithStatus(candidate, OpportunityCandidateStatus.Monitoring)
                        : candidate).ToArray()
            };
            await repository.SaveAsync(directory, reviewedState, CancellationToken.None);

            OpportunityDiscoveryResult second = await pipeline.RunAsync(
                request,
                CancellationToken.None);

            Assert.Equal(0, second.Run.NewCandidates);
            Assert.Equal(29, second.Run.UpdatedCandidates);
            Assert.Equal(
                OpportunityCandidateStatus.Monitoring,
                second.State.Candidates.Single(candidate => candidate.Id == reviewed.Id).Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Configuration_ValidatesOfflineAndLiveRadarProfiles()
    {
        ConfigurationLoader loader = new();
        OpportunityDiscoveryCatalog offline = await loader.LoadOpportunityCatalogAsync(
            ConfigPath("discovery-sources.json"),
            CancellationToken.None);
        OpportunityDiscoveryCatalog live = await loader.LoadOpportunityCatalogAsync(
            ConfigPath("discovery-sources.live.json"),
            CancellationToken.None);

        IReadOnlyList<MunicipalityDefinition> municipalities =
            await loader.LoadMunicipalitiesAsync(
                ConfigPath("municipalities.json"),
                CancellationToken.None);

        Assert.Empty(loader.ValidateOpportunityCatalog(offline, municipalities));
        Assert.Empty(loader.ValidateOpportunityCatalog(live, municipalities));
        Assert.All(offline.Sources, source => Assert.NotNull(source.FixturePath));
        Assert.All(live.Sources, source => Assert.Null(source.FixturePath));
    }

    [Fact]
    public async Task OpportunityState_RecoversFromSecondAtomicBackup()
    {
        string directory = CreateTempDirectory();
        try
        {
            JsonOpportunityStateRepository repository = new();
            await repository.SaveAsync(
                directory,
                StateWithTitle("Primera"),
                CancellationToken.None);
            await repository.SaveAsync(
                directory,
                StateWithTitle("Segunda"),
                CancellationToken.None);
            await repository.SaveAsync(
                directory,
                StateWithTitle("Tercera"),
                CancellationToken.None);

            await File.WriteAllTextAsync(
                Path.Combine(directory, "opportunity-candidates.json"),
                "{corrupt");
            await File.WriteAllTextAsync(
                Path.Combine(directory, "opportunity-candidates.backup-1.json"),
                "null");

            OpportunityRadarState recovered = await repository.LoadAsync(
                directory,
                CancellationToken.None);

            Assert.Equal("Primera", Assert.Single(recovered.Candidates).Title);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Pipeline_RejectsAnUnboundedBackfillWindow()
    {
        OpportunityDiscoveryPipeline pipeline = new(
            new UnusedFeedReader(),
            new UnusedStateRepository(),
            new FixedClock(new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero)));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => pipeline.RunAsync(
                new()
                {
                    From = new(2025, 1, 1),
                    To = new(2026, 5, 21)
                },
                CancellationToken.None));

        Assert.Contains("367 días", exception.Message, StringComparison.Ordinal);
    }

    private static OpportunityCandidate CopyWithStatus(
        OpportunityCandidate candidate,
        OpportunityCandidateStatus status)
    {
        return new()
        {
            Id = candidate.Id,
            SourceId = candidate.SourceId,
            SourceName = candidate.SourceName,
            SourceKind = candidate.SourceKind,
            ExternalId = candidate.ExternalId,
            Title = candidate.Title,
            Summary = candidate.Summary,
            OfficialUrl = candidate.OfficialUrl,
            PublishedAtUtc = candidate.PublishedAtUtc,
            Municipality = candidate.Municipality,
            Kind = candidate.Kind,
            Confidence = candidate.Confidence,
            MatchedTerms = candidate.MatchedTerms,
            FirstSeenUtc = candidate.FirstSeenUtc,
            LastSeenUtc = candidate.LastSeenUtc,
            Status = status
        };
    }

    private static OpportunityRadarState StateWithTitle(string title)
    {
        return new()
        {
            UpdatedAtUtc = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero),
            Candidates =
            [
                new()
                {
                    Id = "lead-test",
                    SourceId = "test",
                    SourceName = "Test",
                    ExternalId = "external",
                    Title = title,
                    OfficialUrl = "https://official.example/item",
                    Municipality = "Galapagar",
                    FirstSeenUtc = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero),
                    LastSeenUtc = new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero)
                }
            ]
        };
    }

    private static string FixturePath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "test-data", "discovery", file);
    }

    private static string ConfigPath(string file)
    {
        return Path.Combine(AppContext.BaseDirectory, "config", file);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"sierranueva-opportunities-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class UnusedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            throw new InvalidOperationException("Las fixtures no deben usar red.");
        }
    }

    private sealed class RecordingHttpClientFactory(byte[] summary) :
        IHttpClientFactory,
        IDisposable
    {
        private readonly HttpMessageHandler _handler = new RecordingHandler(summary);

        public List<Uri> Requests { get; } = [];

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(
                new DelegatingRecorder(_handler, Requests),
                disposeHandler: true);
        }

        public void Dispose()
        {
            _handler.Dispose();
        }

        private sealed class DelegatingRecorder(
            HttpMessageHandler innerHandler,
            ICollection<Uri> requests) : DelegatingHandler(innerHandler)
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                requests.Add(request.RequestUri!);
                return base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class RecordingHandler(byte[] summary) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                bool isCalendar = request.RequestUri!.AbsolutePath.Equals(
                    "/search-day-month",
                    StringComparison.Ordinal);
                HttpContent content = isCalendar
                    ? new StringContent(
                        """
                        <html><body>
                          <a href="/boletin/CM_Boletin_BOCM/2026/05/21/BOCM-20260521120.xml">
                            Sumario XML
                          </a>
                        </body></html>
                        """)
                    : new ByteArrayContent(summary);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = content
                });
            }
        }
    }

    private sealed class StaticHttpClientFactory(string content, string mediaType) :
        IHttpClientFactory,
        IDisposable
    {
        private readonly HttpMessageHandler _handler = new StaticHandler(content, mediaType);

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false);
        }

        public void Dispose()
        {
            _handler.Dispose();
        }

        private sealed class StaticHandler(string content, string mediaType) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                StringContent responseContent = new(content);
                responseContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = responseContent
                });
            }
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class UnusedFeedReader : IOpportunityFeedReader
    {
        public Task<IReadOnlyList<OpportunityFeedItem>> ReadAsync(
            OpportunitySourceDefinition source,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No debe leer feeds.");
        }
    }

    private sealed class UnusedStateRepository : IOpportunityStateRepository
    {
        public Task<OpportunityRadarState> LoadAsync(
            string stateDirectory,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No debe leer estado.");
        }

        public Task SaveAsync(
            string stateDirectory,
            OpportunityRadarState state,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No debe escribir estado.");
        }
    }
}
