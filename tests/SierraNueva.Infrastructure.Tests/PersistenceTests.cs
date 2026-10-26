using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Persistence;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void CsvExport_EscapesTextAndUsesInvariantNumbers()
    {
        Promotion promotion = CreatePromotion();
        promotion.Name = "Residencial \"Cumbre\", fase 2";

        string csv = PublicDataWriter.BuildCsv([promotion]);

        Assert.Contains("\"Residencial \"\"Cumbre\"\", fase 2\"", csv, StringComparison.Ordinal);
        Assert.Contains(",475000,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void GeoJson_ContainsOnlyLocatedPromotions()
    {
        Promotion located = CreatePromotion();
        located.Latitude = 40.67;
        located.Longitude = -3.97;
        Promotion unlocated = CreatePromotion();
        unlocated.Id = "sn-unlocated";

        string json = PublicDataWriter.BuildGeoJson([located, unlocated]);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement feature = Assert.Single(
            document.RootElement.GetProperty("features").EnumerateArray());
        Assert.Equal(located.Id, feature.GetProperty("id").GetString());
        Assert.Equal("Point", feature.GetProperty("geometry").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Writer_CreatesAllPublicArtifactsAtomically()
    {
        string directory = CreateTempDirectory();
        try
        {
            PublicDataWriter writer = new();
            Promotion promotion = CreatePromotion();
            await writer.WriteAsync(
                directory,
                new PromotionDataset
                {
                    RunId = "run",
                    GeneratedAtUtc = DateTimeOffset.UtcNow,
                    Promotions = [promotion]
                },
                new ChangeDataset { RunId = "run" },
                new RunReport { RunId = "run" },
                CancellationToken.None);

            string[] expected =
            [
                "promotions.json",
                "promotions.csv",
                "promotions.geojson",
                "changes.json",
                "run.json"
            ];
            Assert.All(expected, file => Assert.True(File.Exists(Path.Combine(directory, file))));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StateRepository_RecoversFromRotatedBackups()
    {
        string directory = CreateTempDirectory();
        try
        {
            JsonPromotionStateRepository repository = new();
            Promotion first = CreatePromotion();
            first.Name = "Primera versión";
            Promotion second = CreatePromotion();
            second.Name = "Segunda versión";
            Promotion third = CreatePromotion();
            third.Name = "Tercera versión";

            await repository.SaveAsync(directory, [first], CancellationToken.None);
            await repository.SaveAsync(directory, [second], CancellationToken.None);
            await repository.SaveAsync(directory, [third], CancellationToken.None);

            string current = Path.Combine(directory, "promotions-state.json");
            string backupOne = Path.Combine(
                directory,
                "promotions-state.backup-1.json");
            await File.WriteAllTextAsync(current, "{corrupt");

            Promotion recovered = Assert.Single(
                await repository.LoadAsync(directory, CancellationToken.None));
            Assert.Equal("Segunda versión", recovered.Name);

            await File.WriteAllTextAsync(backupOne, "null");
            recovered = Assert.Single(
                await repository.LoadAsync(directory, CancellationToken.None));
            Assert.Equal("Primera versión", recovered.Name);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StateRepository_FailsWithoutOverwritingWhenEveryCopyIsCorrupt()
    {
        string directory = CreateTempDirectory();
        try
        {
            string current = Path.Combine(directory, "promotions-state.json");
            string backupOne = Path.Combine(
                directory,
                "promotions-state.backup-1.json");
            string backupTwo = Path.Combine(
                directory,
                "promotions-state.backup-2.json");
            await File.WriteAllTextAsync(current, "{current");
            await File.WriteAllTextAsync(backupOne, "{backup-one");
            await File.WriteAllTextAsync(backupTwo, "null");

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => new JsonPromotionStateRepository().LoadAsync(
                    directory,
                    CancellationToken.None));

            Assert.Contains("todas sus copias", exception.Message, StringComparison.Ordinal);
            Assert.Equal("{current", await File.ReadAllTextAsync(current));
            Assert.Equal("{backup-one", await File.ReadAllTextAsync(backupOne));
            Assert.Equal("null", await File.ReadAllTextAsync(backupTwo));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StateRepository_RecoversExpiredCacheFromPublicDataWithoutChangingItsHistory()
    {
        string directory = CreateTempDirectory();
        try
        {
            Promotion previous = CreatePromotion();
            previous.FirstSeenUtc = previous.LastSeenUtc.AddMonths(-2);
            previous.LastChangedUtc = previous.LastSeenUtc.AddDays(-7);
            previous.Active = false;
            previous.ConsecutiveMisses = 3;
            string publicPath = await WritePublicRecoveryAsync(directory, previous);
            string original = await File.ReadAllTextAsync(publicPath);
            string missingState = Path.Combine(directory, "expired-cache");
            JsonPromotionStateRepository repository = new(publicDataDirectory: directory);

            Promotion recovered = Assert.Single(
                await repository.LoadAsync(missingState, CancellationToken.None));

            Assert.Equal(previous.Id, recovered.Id);
            Assert.Equal(previous.FirstSeenUtc, recovered.FirstSeenUtc);
            Assert.Equal(previous.LastSeenUtc, recovered.LastSeenUtc);
            Assert.Equal(previous.LastChangedUtc, recovered.LastChangedUtc);
            Assert.Equal(3, recovered.ConsecutiveMisses);
            Assert.False(recovered.Active);
            Assert.False(Directory.Exists(missingState));
            Assert.Equal(original, await File.ReadAllTextAsync(publicPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StateRepository_PrefersPrivateStateAndNeverMasksItsCorruptionWithPublicData()
    {
        string directory = CreateTempDirectory();
        try
        {
            Promotion snapshot = CreatePromotion();
            snapshot.Name = "Snapshot público";
            await WritePublicRecoveryAsync(directory, snapshot);
            Promotion current = CreatePromotion();
            current.Name = "Estado privado actual";
            string state = Path.Combine(directory, "state");
            JsonPromotionStateRepository repository = new(publicDataDirectory: directory);
            await repository.SaveAsync(state, [current], CancellationToken.None);

            Promotion recovered = Assert.Single(await repository.LoadAsync(state, CancellationToken.None));
            Assert.Equal(current.Name, recovered.Name);

            string statePath = Path.Combine(state, "promotions-state.json");
            await File.WriteAllTextAsync(statePath, "{corrupt");
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                repository.LoadAsync(state, CancellationToken.None));
            Assert.Equal("{corrupt", await File.ReadAllTextAsync(statePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"schemaVersion":"2.0","runId":"old","generatedAtUtc":"2026-09-27T05:00:00Z","promotions":[]}""")]
    [InlineData("""{"schemaVersion":"1.0","runId":"old","generatedAtUtc":"2026-09-27T05:00:00Z","promotions":[]}""")]
    public async Task StateRepository_RejectsAnInvalidPublicRecoveryInsteadOfStartingEmpty(string json)
    {
        string directory = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "promotions.json"), json);
            JsonPromotionStateRepository repository = new(publicDataDirectory: directory);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                repository.LoadAsync(Path.Combine(directory, "state"), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OpportunityReports_AreWrittenAtomicallyInsidePrivateState()
    {
        string directory = CreateTempDirectory();
        try
        {
            JsonOpportunityReportWriter writer = new();
            await writer.SaveBackfillAsync(
                directory,
                new OpportunityBackfillReport
                {
                    SourceId = "bocm-calendar",
                    From = new(2025, 1, 1),
                    To = new(2025, 12, 31),
                    BatchDays = 367,
                    Complete = true
                },
                CancellationToken.None);
            await writer.SaveAuditAsync(
                directory,
                new OpportunityAuditReport
                {
                    Population = 29,
                    RequestedSampleSize = 10,
                    ActualSampleSize = 10
                },
                CancellationToken.None);

            string backfillPath = Path.Combine(
                directory,
                JsonOpportunityReportWriter.BackfillFileName);
            string auditPath = Path.Combine(
                directory,
                JsonOpportunityReportWriter.AuditFileName);
            Assert.True(File.Exists(backfillPath));
            Assert.True(File.Exists(auditPath));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
            using JsonDocument backfill = JsonDocument.Parse(
                await File.ReadAllTextAsync(backfillPath));
            Assert.Equal(
                "bocm-calendar",
                backfill.RootElement.GetProperty("sourceId").GetString());
            using JsonDocument audit = JsonDocument.Parse(
                await File.ReadAllTextAsync(auditPath));
            Assert.Equal(29, audit.RootElement.GetProperty("population").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<string> WritePublicRecoveryAsync(string directory, Promotion promotion)
    {
        string path = Path.Combine(directory, "promotions.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new PromotionDataset
        {
            RunId = "previous-run",
            GeneratedAtUtc = promotion.LastSeenUtc,
            Promotions = [promotion]
        }, JsonDefaults.Indented));
        return path;
    }

    private static Promotion CreatePromotion()
    {
        return new()
        {
            Id = "sn-test",
            Name = "Residencial Cumbre",
            NormalizedName = "residencial cumbre",
            Municipality = "Moralzarzal",
            CanonicalUrl = "https://example.com/cumbre",
            SourceUrls = ["https://example.com/cumbre"],
            PriceFrom = 475_000m,
            SourceConfidence = 0.9m,
            Active = true,
            LastSeenUtc = DateTimeOffset.UtcNow
        };
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sierranueva-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
