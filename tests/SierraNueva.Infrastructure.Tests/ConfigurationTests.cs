using System.Globalization;
using SierraNueva.Contracts;
using SierraNueva.Infrastructure.Configuration;

namespace SierraNueva.Infrastructure.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Validation_RequiresMatchingProvenanceForEveryPublishedCentroid()
    {
        MunicipalityDefinition municipality = new()
        {
            OfficialName = "Moralzarzal",
            Latitude = 40.675,
            Longitude = -3.96944444
        };
        MunicipalityCentroidCatalog catalog = new()
        {
            SchemaVersion = "1.0",
            CoordinateReferenceSystem = "WGS84",
            Sources =
            [
                new()
                {
                    Municipality = "Moralzarzal",
                    Latitude = 40.675,
                    Longitude = -3.96944444,
                    SourceUrl = "https://example.test/centroide",
                    CheckedAtUtc = new DateTimeOffset(
                        2026,
                        7,
                        23,
                        0,
                        0,
                        0,
                        TimeSpan.Zero)
                }
            ]
        };

        IReadOnlyList<string> errors = new ConfigurationLoader().Validate(
            new CrawlerSettings(),
            [],
            [municipality],
            catalog);

        Assert.Empty(errors);

        MunicipalityCentroidCatalog mismatched = new()
        {
            SchemaVersion = catalog.SchemaVersion,
            CoordinateReferenceSystem = catalog.CoordinateReferenceSystem,
            Sources =
            [
                new()
                {
                    Municipality = "Moralzarzal",
                    Latitude = 40.676,
                    Longitude = -3.96944444,
                    SourceUrl = "https://example.test/centroide",
                    CheckedAtUtc = catalog.Sources[0].CheckedAtUtc
                }
            ]
        };

        errors = new ConfigurationLoader().Validate(
            new CrawlerSettings(),
            [],
            [municipality],
            mismatched);

        Assert.Contains(
            errors,
            error => error.Contains("no coinciden", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_ReportsDuplicateMunicipalitiesWithoutThrowing()
    {
        MunicipalityDefinition first = new() { OfficialName = "Moralzarzal" };
        MunicipalityDefinition duplicate = new() { OfficialName = "moralzarzal" };

        IReadOnlyList<string> errors = new ConfigurationLoader().Validate(
            new CrawlerSettings(),
            [],
            [first, duplicate],
            new()
            {
                SchemaVersion = "1.0",
                CoordinateReferenceSystem = "WGS84"
            });

        Assert.Contains(
            errors,
            error => error.Contains("Municipio duplicado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VersionedIgnFixture_MatchesEveryConfiguredMunicipality()
    {
        string configDirectory = Path.Combine(AppContext.BaseDirectory, "config");
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "test-data",
            "geography",
            "ign-ngmep-municipalities-madrid-2026.csv");
        ConfigurationLoader loader = new();
        IReadOnlyList<MunicipalityDefinition> municipalities =
            await loader.LoadMunicipalitiesAsync(
                Path.Combine(configDirectory, "municipalities.json"),
                CancellationToken.None);
        MunicipalityCentroidCatalog catalog = await loader.LoadCentroidSourcesAsync(
            Path.Combine(configDirectory, "municipality-centroids.json"),
            CancellationToken.None);

        string[] lines = await File.ReadAllLinesAsync(fixturePath);
        Dictionary<string, string[]> fixture = lines
            .Skip(1)
            .Select(line => line.Split(';'))
            .ToDictionary(fields => fields[1], StringComparer.Ordinal);

        Assert.Equal(29, municipalities.Count);
        Assert.Equal(municipalities.Count, catalog.Sources.Count);
        Assert.Equal(municipalities.Count, fixture.Count);
        Assert.Equal("ETRS89", catalog.CoordinateReferenceSystem);
        Assert.Equal("CC-BY 4.0", catalog.License);
        Assert.Equal(
            "496e3079d3b1844e2827d9dfc328fcd2e629e72c2640b46840daa3711b915116",
            catalog.SourceFileSha256);

        foreach (MunicipalityDefinition municipality in municipalities)
        {
            string[] fields = fixture[municipality.OfficialName];
            MunicipalityCentroidSource source = Assert.Single(
                catalog.Sources,
                item => item.Municipality == municipality.OfficialName);
            double longitude = double.Parse(fields[2], CultureInfo.InvariantCulture);
            double latitude = double.Parse(fields[3], CultureInfo.InvariantCulture);

            Assert.Equal(fields[0], source.SourceRecordId);
            Assert.Equal(fields[4], source.CoordinateOrigin);
            Assert.Equal(latitude, source.Latitude);
            Assert.Equal(longitude, source.Longitude);
            Assert.Equal(latitude, municipality.Latitude);
            Assert.Equal(longitude, municipality.Longitude);
        }
    }
}
