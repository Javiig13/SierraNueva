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
}
