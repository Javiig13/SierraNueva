using System.IO.Compression;
using System.Net;
using System.Text.Json;
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

    [Fact]
    public async Task Reader_ParsesAndFiltersSearxngFixtureWithoutNetwork()
    {
        OpportunitySourceDefinition source = SearchSource(
            FixturePath("searxng-search.json"));
        OpportunityFeedReader reader = new(
            new UnusedHttpClientFactory(),
            new OpportunityFeedParser());

        OpportunityFeedItem item = Assert.Single(await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [new() { OfficialName = "Galapagar" }],
            CancellationToken.None));

        Assert.Equal("Galapagar", item.MunicipalityHint);
        Assert.Equal(
            "https://promotora-fixture.test/promocion-bosque-galapagar",
            item.OfficialUrl);
        Assert.Equal([item.OfficialUrl], item.RelatedUrls);
        Assert.Equal(
            new DateTimeOffset(2026, 5, 20, 9, 15, 0, TimeSpan.Zero),
            item.PublishedAtUtc);
    }

    [Fact]
    public async Task Reader_ExecutesEveryMunicipalityAndQueryAndDeduplicatesResults()
    {
        OpportunitySourceDefinition source = SearchSource(
            fixturePath: null,
            searchQueryTemplates:
            [
                "\"{municipality}\" obra nueva",
                "\"{municipality}\" promoción de viviendas"
            ]);
        using SearxngHttpClientFactory factory = new();
        OpportunityFeedReader reader = new(factory, new OpportunityFeedParser());

        IReadOnlyList<OpportunityFeedItem> items = await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [
                new() { OfficialName = "Alpedrete" },
                new() { OfficialName = "Galapagar" }
            ],
            CancellationToken.None);

        Assert.Equal(4, factory.Requests.Count);
        Assert.Equal(2, items.Count);
        Assert.Equal(
            ["Alpedrete", "Galapagar"],
            items.Select(item => item.MunicipalityHint!).Order().ToArray());
        Assert.All(
            factory.Requests,
            request => Assert.Equal("opportunity-search", request.ClientName));
        Assert.Equal(
            4,
            factory.Requests
                .Select(request => request.Uri.Query)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Theory]
    [InlineData("bocm-rss.xml", OpportunityFeedFormat.Rss, 2)]
    [InlineData("boe-sumario.json", OpportunityFeedFormat.BoeJson, 2)]
    [InlineData("pcsp-licitaciones.atom", OpportunityFeedFormat.Atom, 3)]
    [InlineData("portal-suelo.html", OpportunityFeedFormat.Html, 2)]
    [InlineData("bocm-sumario.xml", OpportunityFeedFormat.BocmCalendar, 2)]
    [InlineData("eadmin-tablon.html", OpportunityFeedFormat.EAdminHtml, 2)]
    [InlineData("cercedilla-noticias.rss.xml", OpportunityFeedFormat.Rss, 2)]
    [InlineData("collado-villalba-actualidad.html", OpportunityFeedFormat.Html, 2)]
    [InlineData("guadalix-noticias.rss.xml", OpportunityFeedFormat.Rss, 2)]
    [InlineData("navalafuente-noticias.rss.xml", OpportunityFeedFormat.Rss, 2)]
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
            ItemSelectors =
                [".component-carousel-item", ".accordion-content li", ".carousel-item"],
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
            [],
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
                [],
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
    public async Task Reader_ReadsOfficialSitemapAndRejectsNonHttpsOrForeignUrls()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "promoter-sitemap",
            Name = "Sitemap de promotora",
            Enabled = true,
            SourceKind = OpportunitySourceKind.OfficialCommercialWebsite,
            Format = OpportunityFeedFormat.Sitemap,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate = "https://promotora-fixture.test/sitemap.xml",
            FixturePath = FixturePath("promoter-sitemap.xml"),
            AllowedHosts = ["promotora-fixture.test"],
            MaxItems = 100
        };
        OpportunityFeedReader reader = new(
            new UnusedHttpClientFactory(),
            new OpportunityFeedParser());

        IReadOnlyList<OpportunityFeedItem> items = await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [],
            CancellationToken.None);

        Assert.Equal(2, items.Count);
        OpportunityFeedItem promotion = Assert.Single(
            items,
            item => item.OfficialUrl.Contains(
                "residencial-encinar",
                StringComparison.Ordinal));
        Assert.Contains("obra nueva", promotion.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            new DateTimeOffset(2026, 5, 20, 8, 30, 0, TimeSpan.Zero),
            promotion.PublishedAtUtc);
        Assert.All(
            items,
            item => Assert.StartsWith(
                "https://promotora-fixture.test/",
                item.OfficialUrl,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reader_FollowsOnlyAllowedSitemapsFromAnOfficialIndex()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "promoter-sitemap-index",
            Name = "Índice sitemap de promotora",
            Enabled = true,
            SourceKind = OpportunitySourceKind.OfficialCommercialWebsite,
            Format = OpportunityFeedFormat.Sitemap,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate = "https://promotora-fixture.test/sitemap-index.xml",
            AllowedHosts = ["promotora-fixture.test"],
            SitemapIncludes = ["property-sitemap"],
            MaxItems = 100
        };
        Dictionary<string, byte[]> responses = new(StringComparer.Ordinal)
        {
            ["https://promotora-fixture.test/sitemap-index.xml"] =
                await File.ReadAllBytesAsync(FixturePath("promoter-sitemap-index.xml")),
            ["https://promotora-fixture.test/property-sitemap.xml"] =
                await File.ReadAllBytesAsync(FixturePath("promoter-sitemap.xml"))
        };
        using MappingHttpClientFactory factory = new(responses);
        OpportunityFeedReader reader = new(factory, new OpportunityFeedParser());

        IReadOnlyList<OpportunityFeedItem> items = await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [],
            CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal(
            [
                "https://promotora-fixture.test/sitemap-index.xml",
                "https://promotora-fixture.test/property-sitemap.xml"
            ],
            factory.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [Fact]
    public async Task Reader_FollowsOnlyScopedDirectoryDetailsAndCapturesExternalLinks()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "industry-directory",
            Name = "Directorio sectorial",
            Enabled = true,
            SourceKind = OpportunitySourceKind.IndustryDirectory,
            Format = OpportunityFeedFormat.Sitemap,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate = "https://industry-fixture.test/sitemap-index.xml",
            AllowedHosts = ["industry-fixture.test"],
            SitemapIncludes = ["oferta-sitemap"],
            FollowDetailPages = true,
            DetailUrlIncludes = ["galapagar"],
            DetailContentSelectors =
                [".migas-header", ".oferta-datas.descripcion", ".single-offer-main-exhibitor"],
            DetailLinkSelectors =
                ["a.virtual[href^='https://']", ".oferta-datas.descripcion a[href^='https://']"],
            MaxDetailPages = 5,
            MaxItems = 100
        };
        Dictionary<string, byte[]> responses = new(StringComparer.Ordinal)
        {
            ["https://industry-fixture.test/sitemap-index.xml"] =
                await File.ReadAllBytesAsync(
                    FixturePath("industry-directory-sitemap-index.xml")),
            ["https://industry-fixture.test/oferta-sitemap.xml"] =
                await File.ReadAllBytesAsync(
                    FixturePath("industry-directory-offers.xml")),
            ["https://industry-fixture.test/oferta/residencial-encinar-galapagar/"] =
                await File.ReadAllBytesAsync(
                    FixturePath("industry-directory-detail.html"))
        };
        using MappingHttpClientFactory factory = new(responses);
        OpportunityFeedReader reader = new(factory, new OpportunityFeedParser());

        OpportunityFeedItem item = Assert.Single(await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [],
            CancellationToken.None));

        Assert.Contains("Galapagar", item.Summary, StringComparison.Ordinal);
        Assert.Equal(
            ["https://promotora-nueva.test/encinar"],
            item.RelatedUrls);
        Assert.Equal(
            [
                "https://industry-fixture.test/sitemap-index.xml",
                "https://industry-fixture.test/oferta-sitemap.xml",
                "https://industry-fixture.test/oferta/residencial-encinar-galapagar/"
            ],
            factory.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [Fact]
    public async Task Reader_FollowsDirectoryDetailsFoundInABoundedHtmlIndex()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "industry-directory-index",
            Name = "Directorio sectorial — Galapagar",
            Enabled = true,
            SourceKind = OpportunitySourceKind.IndustryDirectory,
            Format = OpportunityFeedFormat.HtmlLinks,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate = "https://industry-fixture.test/oferta/?localizacion=Galapagar",
            AllowedHosts = ["industry-fixture.test"],
            ItemSelectors =
                [".listado-oferta .oferta-content h2 a[href*='/oferta/']"],
            FollowDetailPages = true,
            DetailUrlIncludes = ["/oferta/"],
            DetailContentSelectors =
                [".migas-header", ".oferta-datas.descripcion", ".single-offer-main-exhibitor"],
            DetailLinkSelectors =
                ["a.virtual[href^='https://']", ".oferta-datas.descripcion a[href^='https://']"],
            MaxDetailPages = 5,
            MaxItems = 10
        };
        Dictionary<string, byte[]> responses = new(StringComparer.Ordinal)
        {
            ["https://industry-fixture.test/oferta/?localizacion=Galapagar"] =
                await File.ReadAllBytesAsync(
                    FixturePath("industry-directory-index.html")),
            ["https://industry-fixture.test/oferta/residencial-encinar/"] =
                await File.ReadAllBytesAsync(
                    FixturePath("industry-directory-detail.html"))
        };
        using MappingHttpClientFactory factory = new(responses);
        OpportunityFeedReader reader = new(factory, new OpportunityFeedParser());

        OpportunityFeedItem item = Assert.Single(await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [],
            CancellationToken.None));

        Assert.Contains("Galapagar", item.Summary, StringComparison.Ordinal);
        Assert.Equal(
            ["https://promotora-nueva.test/encinar"],
            item.RelatedUrls);
        Assert.Equal(
            [
                "https://industry-fixture.test/oferta/?localizacion=Galapagar",
                "https://industry-fixture.test/oferta/residencial-encinar/"
            ],
            factory.Requests.Select(uri => uri.AbsoluteUri).ToArray());
    }

    [Fact]
    public async Task Reader_ReadsScopedOfficialInternalLinksAndRejectsUnsafeTargets()
    {
        OpportunitySourceDefinition source = new()
        {
            Id = "promoter-links",
            Name = "Promotora oficial — promociones",
            Enabled = true,
            SourceKind = OpportunitySourceKind.OfficialCommercialWebsite,
            Format = OpportunityFeedFormat.HtmlLinks,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate = "https://promotora-fixture.test/",
            FixturePath = FixturePath("promoter-internal-links.html"),
            AllowedHosts = ["promotora-fixture.test"],
            ItemSelectors = [".promotion-card a[href]"],
            MaxItems = 10
        };
        OpportunityFeedReader reader = new(
            new UnusedHttpClientFactory(),
            new OpportunityFeedParser());

        IReadOnlyList<OpportunityFeedItem> items = await reader.ReadAsync(
            source,
            FixtureDate,
            FixtureDate,
            [],
            CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Contains(
            items,
            item => item.OfficialUrl.EndsWith(
                "/promociones/residencial-encinar-galapagar/",
                StringComparison.Ordinal));
        OpportunityFeedItem robledo = Assert.Single(
            items,
            item => item.OfficialUrl.Contains(
                "robledo-de-chavela",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            "Villalba",
            robledo.Summary,
            StringComparison.OrdinalIgnoreCase);
        Assert.All(
            items,
            item =>
            {
                Assert.StartsWith(
                    "https://promotora-fixture.test/",
                    item.OfficialUrl,
                    StringComparison.Ordinal);
                Assert.Contains(
                    "promociones",
                    item.Title,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task Pipeline_AppliesReviewedDispositionToAnExistingCandidate()
    {
        string directory = CreateTempDirectory();
        try
        {
            OpportunityFeedItem item = new()
            {
                ExternalId = "promotion-1",
                Title = "Promoción de viviendas en Galapagar",
                Summary = "Promoción residencial de viviendas",
                OfficialUrl = "https://official.example/promociones/galapagar/"
            };
            ScriptedFeedReader reader = new(
                new OpportunityFeedItem[] { item },
                new OpportunityFeedItem[] { item });
            OpportunityDiscoveryPipeline pipeline = new(
                reader,
                new JsonOpportunityStateRepository(),
                new FixedClock(new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero)));
            OpportunitySourceDefinition initialSource = new()
            {
                Id = "official-links",
                Name = "Promociones oficiales",
                Enabled = true,
                SourceKind = OpportunitySourceKind.OfficialCommercialWebsite,
                Format = OpportunityFeedFormat.HtmlLinks
            };
            OpportunitySourceDefinition reviewedSource = new()
            {
                Id = initialSource.Id,
                Name = initialSource.Name,
                Enabled = true,
                SourceKind = initialSource.SourceKind,
                Format = initialSource.Format,
                ReviewRules =
                [
                    new()
                    {
                        UrlPattern = "/promociones/galapagar/",
                        Status = OpportunityCandidateStatus.Rejected
                    }
                ]
            };
            OpportunityDiscoveryCatalog CatalogFor(
                OpportunitySourceDefinition source)
            {
                return new()
                {
                    Terms =
                    [
                        new()
                        {
                            Term = "promoción",
                            Kind = OpportunityKind.ResidentialDevelopment
                        }
                    ],
                    ContextTerms = ["viviendas"],
                    Sources = [source]
                };
            }

            OpportunityDiscoveryRequest request = new()
            {
                Catalog = CatalogFor(initialSource),
                Municipalities = [new() { OfficialName = "Galapagar" }],
                StateDirectory = directory,
                From = FixtureDate,
                To = FixtureDate
            };
            OpportunityDiscoveryResult first = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            Assert.Equal(
                OpportunityCandidateStatus.New,
                Assert.Single(first.State.Candidates).Status);

            OpportunityDiscoveryResult second = await pipeline.RunAsync(
                new()
                {
                    Catalog = CatalogFor(reviewedSource),
                    Municipalities = request.Municipalities,
                    StateDirectory = directory,
                    From = FixtureDate,
                    To = FixtureDate
                },
                CancellationToken.None);

            Assert.Equal(
                OpportunityCandidateStatus.Rejected,
                Assert.Single(second.State.Candidates).Status);
            Assert.Equal(0, second.State.Coverage.PendingCandidates);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

            Assert.Equal(34, first.Run.NewCandidates);
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
                    "Guadalix de la Sierra",
                    "Guadarrama",
                    "Hoyo de Manzanares",
                    "La Cabrera",
                    "Los Molinos",
                    "Manzanares el Real",
                    "Miraflores de la Sierra",
                    "Moralzarzal",
                    "Navacerrada",
                    "Navalafuente",
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
            OpportunityCandidate searchCandidate = Assert.Single(
                first.State.Candidates,
                candidate => candidate.SourceKind == OpportunitySourceKind.WebSearch);
            Assert.Equal("Galapagar", searchCandidate.Municipality);
            Assert.InRange(searchCandidate.Confidence, 0.45m, 0.7m);
            Assert.Equal(
                OpportunityCandidateStatus.New,
                searchCandidate.Status);
            Assert.Equal(34, first.State.SourceHealth.Count);
            Assert.All(
                first.State.SourceHealth,
                source => Assert.Equal(
                    OpportunitySourceHealthStatus.Healthy,
                    source.Status));
            Assert.Equal(29, first.State.Coverage.MunicipalitiesTotal);
            Assert.Equal(28, first.State.Coverage.MunicipalitiesWithDirectSource);
            Assert.Equal(28, first.State.Coverage.MunicipalitiesWithHealthyDirectSource);
            Assert.Equal(29, first.State.Coverage.MunicipalitiesWithHealthyCoverage);
            Assert.Equal(34, first.State.Coverage.PendingCandidates);
            Assert.Equal(1, first.State.Coverage.CommercialSources);
            Assert.Equal(1, first.State.Coverage.HealthyCommercialSources);
            Assert.Equal(1, first.State.Coverage.CommercialDomainsMonitored);
            Assert.Equal(1, first.State.Coverage.HealthyCommercialDomains);
            Assert.Equal(34, first.State.Coverage.NewCandidates);
            Assert.Equal(0, first.State.Coverage.MonitoringCandidates);
            Assert.Equal(0, first.State.Coverage.RejectedCandidates);
            Assert.Equal(0, first.State.Coverage.VerifiedSourceCandidates);
            Assert.Equal(0, first.State.Coverage.StaleCandidates);
            Assert.Equal(1, first.State.Coverage.MunicipalitiesWithCommercialSignals);
            Assert.Equal(
                MunicipalityCoverageStatus.CentralOnly,
                first.State.Coverage.Municipalities.Single(
                    item => item.Municipality == "Robledo de Chavela").Status);
            Assert.Equal(
                MunicipalityCoverageStatus.DirectAndCentral,
                first.State.Coverage.Municipalities.Single(
                    item => item.Municipality == "Galapagar").Status);

            OpportunityCandidate reviewed = first.State.Candidates[0];
            OpportunityRadarState reviewedState = new()
            {
                UpdatedAtUtc = first.State.UpdatedAtUtc,
                LastRun = first.Run,
                Candidates = first.State.Candidates.Select(candidate =>
                    candidate.Id == reviewed.Id
                        ? CopyWithStatus(candidate, OpportunityCandidateStatus.Monitoring)
                        : candidate).ToArray(),
                SourceHealth = first.State.SourceHealth,
                Coverage = first.State.Coverage
            };
            await repository.SaveAsync(directory, reviewedState, CancellationToken.None);

            OpportunityDiscoveryResult second = await pipeline.RunAsync(
                request,
                CancellationToken.None);

            Assert.Equal(0, second.Run.NewCandidates);
            Assert.Equal(34, second.Run.UpdatedCandidates);
            Assert.Equal(
                OpportunityCandidateStatus.Monitoring,
                second.State.Candidates.Single(candidate => candidate.Id == reviewed.Id).Status);
            Assert.Equal(34, second.State.SourceHealth.Count);
            Assert.Equal(29, second.State.Coverage.MunicipalitiesWithHealthyCoverage);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Pipeline_TracksEmptyResponsesAndRepeatedFailuresWithoutLosingCoverageState()
    {
        string directory = CreateTempDirectory();
        try
        {
            OpportunitySourceDefinition source = new()
            {
                Id = "galapagar-notices",
                Name = "Tablón de Galapagar",
                Enabled = true,
                SourceKind = OpportunitySourceKind.MunicipalNoticeBoard,
                Format = OpportunityFeedFormat.Rss,
                Cadence = OpportunityFeedCadence.Daily,
                FixedMunicipality = "Galapagar"
            };
            OpportunityDiscoveryCatalog catalog = new()
            {
                Terms =
                [
                    new()
                    {
                        Term = "licencia de obra",
                        Kind = OpportunityKind.BuildingPermit
                    }
                ],
                ContextTerms = ["viviendas"],
                Sources = [source]
            };
            OpportunityFeedItem item = new()
            {
                ExternalId = "notice-1",
                Title = "Licencia de obra para 12 viviendas",
                OfficialUrl = "https://official.example/notices/1"
            };
            ScriptedFeedReader reader = new(
                new OpportunityFeedItem[] { item },
                Array.Empty<OpportunityFeedItem>(),
                Array.Empty<OpportunityFeedItem>(),
                new IOException("Fallo transitorio"),
                new IOException("Fallo repetido"),
                new OpportunityFeedItem[] { item });
            OpportunityDiscoveryPipeline pipeline = new(
                reader,
                new JsonOpportunityStateRepository(),
                new FixedClock(new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero)));
            OpportunityDiscoveryRequest request = new()
            {
                Catalog = catalog,
                Municipalities = [new() { OfficialName = "Galapagar" }],
                StateDirectory = directory,
                From = FixtureDate,
                To = FixtureDate,
                KnownPromotionUrls = ["https://official.example/notices/1/"]
            };

            OpportunityDiscoveryResult first = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            Assert.Equal(
                OpportunitySourceHealthStatus.Healthy,
                Assert.Single(first.State.SourceHealth).Status);
            Assert.Equal(
                OpportunityCandidateStatus.VerifiedSource,
                Assert.Single(first.State.Candidates).Status);
            Assert.Equal(0, first.State.Coverage.PendingCandidates);
            Assert.Equal(
                new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero),
                Assert.Single(first.State.SourceHealth).NextCheckDueUtc);

            OpportunityDiscoveryResult firstEmpty = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            Assert.Equal(
                OpportunitySourceHealthStatus.Healthy,
                Assert.Single(firstEmpty.State.SourceHealth).Status);
            Assert.Equal(1, Assert.Single(firstEmpty.State.SourceHealth).ConsecutiveEmptyRuns);

            OpportunityDiscoveryResult secondEmpty = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            OpportunitySourceHealth emptyHealth = Assert.Single(secondEmpty.State.SourceHealth);
            Assert.Equal(OpportunitySourceHealthStatus.Degraded, emptyHealth.Status);
            Assert.Equal(2, emptyHealth.ConsecutiveEmptyRuns);
            Assert.Contains("cero entradas", Assert.Single(emptyHealth.Issues));
            Assert.Equal(
                MunicipalityCoverageStatus.Degraded,
                Assert.Single(secondEmpty.State.Coverage.Municipalities).Status);

            OpportunityDiscoveryResult firstFailure = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            Assert.Equal(
                OpportunitySourceHealthStatus.Degraded,
                Assert.Single(firstFailure.State.SourceHealth).Status);
            Assert.Equal(1, Assert.Single(firstFailure.State.SourceHealth).ConsecutiveFailures);

            OpportunityDiscoveryResult secondFailure = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            OpportunitySourceHealth failingHealth = Assert.Single(
                secondFailure.State.SourceHealth);
            Assert.Equal(OpportunitySourceHealthStatus.Failing, failingHealth.Status);
            Assert.Equal(2, failingHealth.ConsecutiveFailures);
            Assert.Equal("Fallo repetido", Assert.Single(failingHealth.Issues));

            OpportunityDiscoveryResult recovered = await pipeline.RunAsync(
                request,
                CancellationToken.None);
            OpportunitySourceHealth recoveredHealth = Assert.Single(
                recovered.State.SourceHealth);
            Assert.Equal(OpportunitySourceHealthStatus.Healthy, recoveredHealth.Status);
            Assert.Equal(0, recoveredHealth.ConsecutiveFailures);
            Assert.Equal(0, recoveredHealth.ConsecutiveEmptyRuns);
            Assert.Empty(recoveredHealth.Issues);
            Assert.Equal(
                MunicipalityCoverageStatus.DirectOnly,
                Assert.Single(recovered.State.Coverage.Municipalities).Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Pipeline_ReportsReferencedDomainsThatAreNotYetMonitored()
    {
        string directory = CreateTempDirectory();
        try
        {
            OpportunitySourceDefinition industryDirectory = new()
            {
                Id = "a-industry-directory",
                Name = "Directorio sectorial",
                Enabled = true,
                SourceKind = OpportunitySourceKind.IndustryDirectory,
                Format = OpportunityFeedFormat.Sitemap,
                Cadence = OpportunityFeedCadence.Daily,
                AllowedHosts = ["industry-fixture.test"],
                IgnoreExclusionTerms = true
            };
            OpportunitySourceDefinition knownCommercialSource = new()
            {
                Id = "z-known-promoter",
                Name = "Promotora conocida",
                Enabled = true,
                SourceKind = OpportunitySourceKind.OfficialCommercialWebsite,
                Format = OpportunityFeedFormat.Sitemap,
                Cadence = OpportunityFeedCadence.Daily,
                AllowedHosts = ["www.promotora-conocida.test"]
            };
            OpportunityDiscoveryCatalog catalog = new()
            {
                Terms =
                [
                    new()
                    {
                        Term = "promoción",
                        Kind = OpportunityKind.ResidentialDevelopment
                    }
                ],
                ContextTerms = ["viviendas"],
                ExclusionTerms = ["garaje"],
                Sources = [industryDirectory, knownCommercialSource]
            };
            OpportunityFeedItem item = new()
            {
                ExternalId = "directory-1",
                Title = "Promoción residencial en Galapagar",
                Summary = "Doce viviendas unifamiliares con garaje en Galapagar",
                OfficialUrl = "https://industry-fixture.test/oferta/1",
                RelatedUrls =
                [
                    "https://promotora-conocida.test/promocion",
                    "https://promotora-nueva.test/promocion"
                ]
            };
            OpportunityDiscoveryPipeline pipeline = new(
                new ScriptedFeedReader(
                    new OpportunityFeedItem[] { item },
                    Array.Empty<OpportunityFeedItem>()),
                new JsonOpportunityStateRepository(),
                new FixedClock(new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero)));

            OpportunityDiscoveryResult result = await pipeline.RunAsync(
                new()
                {
                    Catalog = catalog,
                    Municipalities = [new() { OfficialName = "Galapagar" }],
                    StateDirectory = directory,
                    From = FixtureDate,
                    To = FixtureDate
                },
                CancellationToken.None);

            Assert.Equal(2, result.State.Coverage.ReferencedDomainsDiscovered);
            Assert.Equal(1, result.State.Coverage.UnmonitoredReferencedDomains);
            Assert.Equal(2, Assert.Single(result.State.Candidates).RelatedUrls.Count);
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
        Assert.Single(
            offline.Sources,
            source => source.SourceKind ==
                      OpportunitySourceKind.OfficialCommercialWebsite);
        Assert.Equal(
            15,
            live.Sources.Count(source =>
                source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite));
        Assert.Equal(
            14,
            live.Sources.Count(source =>
                source.Format == OpportunityFeedFormat.Sitemap));
        Assert.Equal(
            4,
            live.Sources.Count(source =>
                source.Format == OpportunityFeedFormat.HtmlLinks));
        Assert.All(
            live.Sources.Where(source =>
                source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite),
            source => Assert.True(
                source.Format is
                    OpportunityFeedFormat.Sitemap or
                    OpportunityFeedFormat.HtmlLinks));
        OpportunitySourceDefinition[] directories = live.Sources
            .Where(source =>
                source.SourceKind == OpportunitySourceKind.IndustryDirectory)
            .ToArray();
        Assert.Equal(3, directories.Length);
        Assert.All(directories, source => Assert.True(source.FollowDetailPages));
        Assert.Equal(
            29,
            Assert.Single(
                directories,
                source => source.Format == OpportunityFeedFormat.Sitemap)
                .DetailUrlIncludes.Count);
        OpportunitySourceDefinition offlineSearch = Assert.Single(
            offline.Sources,
            source => source.SourceKind == OpportunitySourceKind.WebSearch);
        OpportunitySourceDefinition liveSearch = Assert.Single(
            live.Sources,
            source => source.SourceKind == OpportunitySourceKind.WebSearch);
        Assert.Equal(OpportunityFeedFormat.SearxngJson, liveSearch.Format);
        Assert.Equal(4, liveSearch.SearchQueryTemplates.Count);
        Assert.Equal(
            116,
            liveSearch.SearchQueryTemplates.Count *
            municipalities.Count(municipality => municipality.Enabled));
        Assert.NotNull(offlineSearch.FixturePath);
        Assert.Null(liveSearch.FixturePath);

        OpportunitySourceDefinition portal =
            Assert.Single(live.Sources, source => source.Id == "portal-suelo-madrid");
        Assert.Contains(
            portal.ReviewRules,
            rule =>
                rule.UrlPattern ==
                "contrataciondelestado.es/wps/portal/plataforma/buscadores/detalle/" &&
                rule.Status == OpportunityCandidateStatus.Monitoring);

        OpportunitySourceDefinition nuvare =
            Assert.Single(live.Sources, source => source.Id == "sitemap-nuvare");
        Assert.Equal(3, nuvare.ReviewRules.Count);
        Assert.All(
            nuvare.ReviewRules,
            rule => Assert.Equal(
                OpportunityCandidateStatus.Rejected,
                rule.Status));

        OpportunitySourceDefinition simaVillalba =
            Assert.Single(
                live.Sources,
                source => source.Id == "sima-collado-villalba-index");
        Assert.Contains(
            simaVillalba.ReviewRules,
            rule =>
                rule.UrlPattern == "/oferta/orbia/" &&
                rule.Status == OpportunityCandidateStatus.Rejected);

        OpportunitySourceDefinition simaEscorial =
            Assert.Single(
                live.Sources,
                source => source.Id == "sima-el-escorial-index");
        Assert.Contains(
            simaEscorial.ReviewRules,
            rule =>
                rule.UrlPattern == "/oferta/nevia/" &&
                rule.Status == OpportunityCandidateStatus.Rejected);
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
            RelatedUrls = candidate.RelatedUrls,
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

    private static OpportunitySourceDefinition SearchSource(
        string? fixturePath,
        IReadOnlyList<string>? searchQueryTemplates = null)
    {
        return new()
        {
            Id = "zz-web-search-matrix",
            Name = "SearXNG privado",
            Enabled = true,
            SourceKind = OpportunitySourceKind.WebSearch,
            Format = OpportunityFeedFormat.SearxngJson,
            Cadence = OpportunityFeedCadence.Daily,
            UrlTemplate =
                "http://127.0.0.1:8888/search?q={query}&format=json",
            FixturePath = fixturePath,
            AllowedHosts = ["127.0.0.1"],
            SearchQueryTemplates = searchQueryTemplates ??
                                   ["\"{municipality}\" obra nueva"],
            ResultExcludedHosts = ["idealista.com"],
            MaxResultsPerQuery = 10,
            SearchDelayMilliseconds = 0,
            MaxItems = 100
        };
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

    private sealed class MappingHttpClientFactory(
        IReadOnlyDictionary<string, byte[]> responses) :
        IHttpClientFactory,
        IDisposable
    {
        private readonly HttpMessageHandler _handler = new MappingHandler(responses);

        public List<Uri> Requests { get; } = [];

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(
                new MappingRecorder(_handler, Requests),
                disposeHandler: true);
        }

        public void Dispose()
        {
            _handler.Dispose();
        }

        private sealed class MappingRecorder(
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

        private sealed class MappingHandler(
            IReadOnlyDictionary<string, byte[]> responses) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (!responses.TryGetValue(
                        request.RequestUri!.AbsoluteUri,
                        out byte[]? content))
                {
                    return Task.FromResult(new HttpResponseMessage(
                        System.Net.HttpStatusCode.NotFound)
                    {
                        RequestMessage = request
                    });
                }

                return Task.FromResult(new HttpResponseMessage(
                    System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new ByteArrayContent(content)
                });
            }
        }
    }

    private sealed class SearxngHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly HttpMessageHandler _handler = new SearxngHandler();

        public List<(string ClientName, Uri Uri)> Requests { get; } = [];

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(
                new SearxngRecorder(_handler, name, Requests),
                disposeHandler: true);
        }

        public void Dispose()
        {
            _handler.Dispose();
        }

        private sealed class SearxngRecorder(
            HttpMessageHandler innerHandler,
            string clientName,
            ICollection<(string ClientName, Uri Uri)> requests) :
            DelegatingHandler(innerHandler)
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                requests.Add((clientName, request.RequestUri!));
                return base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class SearxngHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                string query = ReadQueryParameter(request.RequestUri!, "q");
                string municipality = query.Contains(
                    "Alpedrete",
                    StringComparison.Ordinal)
                    ? "Alpedrete"
                    : "Galapagar";
                string slug = municipality.ToLowerInvariant();
                string response = JsonSerializer.Serialize(new
                {
                    query,
                    results = new[]
                    {
                        new
                        {
                            url = $"https://promotora-fixture.test/{slug}",
                            title = $"Promoción de viviendas en {municipality}",
                            content = "Promoción residencial de obra nueva"
                        }
                    },
                    unresponsive_engines = Array.Empty<string>()
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        response,
                        System.Text.Encoding.UTF8,
                        "application/json")
                });
            }

            private static string ReadQueryParameter(Uri uri, string name)
            {
                return uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair => pair.Split('=', 2))
                    .Where(pair => pair.Length == 2)
                    .Where(pair => string.Equals(
                        pair[0],
                        name,
                        StringComparison.Ordinal))
                    .Select(pair => WebUtility.UrlDecode(pair[1]))
                    .First();
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
            IReadOnlyList<MunicipalityDefinition> municipalities,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No debe leer feeds.");
        }
    }

    private sealed class ScriptedFeedReader(params object[] results) : IOpportunityFeedReader
    {
        private readonly Queue<object> _results = new(results);

        public Task<IReadOnlyList<OpportunityFeedItem>> ReadAsync(
            OpportunitySourceDefinition source,
            DateOnly fromDate,
            DateOnly toDate,
            IReadOnlyList<MunicipalityDefinition> municipalities,
            CancellationToken cancellationToken)
        {
            object result = _results.Dequeue();
            return result switch
            {
                Exception exception => Task.FromException<IReadOnlyList<OpportunityFeedItem>>(
                    exception),
                IReadOnlyList<OpportunityFeedItem> items => Task.FromResult(items),
                _ => throw new InvalidOperationException("Resultado de feed no compatible.")
            };
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
