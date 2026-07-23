using System.Globalization;
using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Geocoding;

public sealed class NominatimGeocoder(
    IHttpClientFactory httpClientFactory,
    NominatimSettings settings,
    string userAgent,
    string stateDirectory,
    IGeocoder fallback) : IGeocoder, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _cachePath = Path.Combine(stateDirectory, "geocoding-cache.json");
    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;

    public async Task<Promotion> GeocodeAsync(
        Promotion promotion,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled ||
            promotion.Latitude.HasValue ||
            string.IsNullOrWhiteSpace(promotion.Address))
        {
            return await fallback.GeocodeAsync(promotion, municipalities, cancellationToken);
        }

        string query = $"{promotion.Address}, {promotion.Municipality}, Madrid, España";
        Dictionary<string, GeocodingCacheEntry> cache = await LoadCacheAsync(cancellationToken);
        if (cache.TryGetValue(query, out GeocodingCacheEntry? cached))
        {
            Apply(promotion, cached);
            return promotion;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan minimumDelay = TimeSpan.FromMinutes(1d / Math.Max(1, settings.MaxRequestsPerMinute));
            TimeSpan remaining = minimumDelay - (DateTimeOffset.UtcNow - _lastRequestUtc);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }

            HttpClient client = httpClientFactory.CreateClient("nominatim");
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"{settings.Endpoint.TrimEnd('/')}/search?format=jsonv2&limit=1&q={Uri.EscapeDataString(query)}");
            request.Headers.UserAgent.ParseAdd(userAgent);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            _lastRequestUtc = DateTimeOffset.UtcNow;
            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement first = document.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object &&
                double.TryParse(
                    first.GetProperty("lat").GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double latitude) &&
                double.TryParse(
                    first.GetProperty("lon").GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double longitude))
            {
                GeocodingCacheEntry entry = new()
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    ResolvedAtUtc = DateTimeOffset.UtcNow,
                    Source = "Nominatim"
                };
                cache[query] = entry;
                await SaveCacheAsync(cache, cancellationToken);
                Apply(promotion, entry);
                return promotion;
            }
        }
        catch (HttpRequestException)
        {
            // Optional geocoding failure falls through to the deterministic centroid.
        }
        finally
        {
            _gate.Release();
        }

        return await fallback.GeocodeAsync(promotion, municipalities, cancellationToken);
    }

    private async Task<Dictionary<string, GeocodingCacheEntry>> LoadCacheAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_cachePath))
        {
            return new(StringComparer.Ordinal);
        }

        await using FileStream stream = File.OpenRead(_cachePath);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, GeocodingCacheEntry>>(
                   stream,
                   JsonDefaults.Compact,
                   cancellationToken)
               ?? new(StringComparer.Ordinal);
    }

    private async Task SaveCacheAsync(
        Dictionary<string, GeocodingCacheEntry> cache,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
        string temporary = $"{_cachePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporary,
            JsonSerializer.Serialize(cache, JsonDefaults.Indented),
            cancellationToken);
        File.Move(temporary, _cachePath, overwrite: true);
    }

    private static void Apply(Promotion promotion, GeocodingCacheEntry entry)
    {
        promotion.Latitude = entry.Latitude;
        promotion.Longitude = entry.Longitude;
        promotion.LocationPrecision = LocationPrecision.Street;
        promotion.Evidence = promotion.Evidence.Append(new EvidenceItem
        {
            Field = "location",
            ValueText = string.Create(
                CultureInfo.InvariantCulture,
                $"{entry.Latitude}, {entry.Longitude}"),
            SourceUrl = promotion.CanonicalUrl,
            CapturedAtUtc = entry.ResolvedAtUtc,
            Extractor = nameof(NominatimGeocoder),
            Confidence = 0.75m,
            Quality = FieldQuality.Normalized,
            TextFragment = "Geocodificación persistida"
        }).ToArray();
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    public sealed class GeocodingCacheEntry
    {
        public double Latitude { get; init; }

        public double Longitude { get; init; }

        public DateTimeOffset ResolvedAtUtc { get; init; }

        public string Source { get; init; } = string.Empty;
    }
}
