using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Infrastructure.Discovery;
using SierraNueva.Infrastructure.Geocoding;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Tests;

public sealed class ContractAndCacheTests
{
    [Fact]
    public void PublicContract_SerializesEnumsAndNumbersPredictably()
    {
        PromotionDataset dataset = new()
        {
            SchemaVersion = "1.0",
            RunId = "run",
            GeneratedAtUtc = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero),
            Promotions =
            [
                new Promotion
                {
                    Id = "sn-1",
                    Name = "Residencial",
                    Municipality = "Moralzarzal",
                    CanonicalUrl = "https://example.com/promo",
                    PriceFrom = 475_000m,
                    CommercialStatus = CommercialStatus.OnSale
                }
            ]
        };

        string json = JsonSerializer.Serialize(dataset, JsonDefaults.Compact);

        Assert.Contains("\"commercialStatus\":\"onSale\"", json, StringComparison.Ordinal);
        Assert.Contains("\"priceFrom\":475000", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"475000\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualDiscovery_ReadsJsonAndIgnoresInvalidValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sierranueva-urls-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                """{"urls":["https://example.com/uno","no-es-url",42]}""");
            ManualFileDiscoveryProvider provider = new();

            IReadOnlyList<Uri> urls = await provider.DiscoverAsync(
                new SourceDefinition { ManualUrlsFile = path },
                CancellationToken.None);

            Uri url = Assert.Single(urls);
            Assert.Equal("https://example.com/uno", url.AbsoluteUri);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NominatimGeocoder_UsesPersistentCacheWithoutNetwork()
    {
        string directory = CreateTempDirectory();
        try
        {
            string query = "Avenida de la Sierra 18, Moralzarzal, Madrid, España";
            Dictionary<string, NominatimGeocoder.GeocodingCacheEntry> cache = new()
            {
                [query] = new()
                {
                    Latitude = 40.6762,
                    Longitude = -3.9711,
                    ResolvedAtUtc = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
                    Source = "Nominatim"
                }
            };
            await File.WriteAllTextAsync(
                Path.Combine(directory, "geocoding-cache.json"),
                JsonSerializer.Serialize(cache, JsonDefaults.Compact));
            using NominatimGeocoder geocoder = new(
                new ThrowingHttpClientFactory(),
                new NominatimSettings { Enabled = true },
                "SierraNueva.Tests/1.0",
                directory,
                new MunicipalityCentroidGeocoder());
            Promotion promotion = new()
            {
                Name = "Residencial",
                Municipality = "Moralzarzal",
                Address = "Avenida de la Sierra 18",
                CanonicalUrl = "https://example.com/promo"
            };

            Promotion result = await geocoder.GeocodeAsync(
                promotion,
                [],
                CancellationToken.None);

            Assert.Equal(40.6762, result.Latitude);
            Assert.Equal(-3.9711, result.Longitude);
            Assert.Equal(LocationPrecision.Street, result.LocationPrecision);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sierranueva-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            throw new InvalidOperationException($"No se esperaba acceso de red para '{name}'.");
        }
    }
}
