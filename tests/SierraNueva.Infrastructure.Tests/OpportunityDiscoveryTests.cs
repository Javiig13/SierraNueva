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

            Assert.Equal(4, first.Run.NewCandidates);
            Assert.Equal(
                ["Collado Villalba", "Galapagar", "Miraflores de la Sierra", "Soto del Real"],
                first.State.Candidates
                    .Select(candidate => candidate.Municipality)
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
            Assert.Equal(4, second.Run.UpdatedCandidates);
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

        Assert.Empty(loader.ValidateOpportunityCatalog(offline));
        Assert.Empty(loader.ValidateOpportunityCatalog(live));
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
