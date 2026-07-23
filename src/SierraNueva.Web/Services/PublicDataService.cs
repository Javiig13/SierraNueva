using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SierraNueva.Contracts;
using SierraNueva.Web.Models;

namespace SierraNueva.Web.Services;

public sealed class PublicDataService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    private readonly string _cacheToken = DateTimeOffset.UtcNow
        .ToUnixTimeMilliseconds()
        .ToString(CultureInfo.InvariantCulture);

    public async Task<SiteData> LoadAsync(CancellationToken cancellationToken)
    {
        PromotionDataset promotions = await LoadAsync<PromotionDataset>(
            Versioned("data/promotions.json"),
            cancellationToken);
        ChangeDataset changes = await LoadAsync<ChangeDataset>(
            Versioned("data/changes.json"),
            cancellationToken);
        RunReport run = await LoadAsync<RunReport>(
            Versioned("data/run.json"),
            cancellationToken);
        using JsonDocument geoJson = JsonDocument.Parse(
            await httpClient.GetStringAsync(
                Versioned("data/promotions.geojson"),
                cancellationToken));
        return new()
        {
            Promotions = promotions,
            Changes = changes,
            Run = run,
            GeoJson = geoJson.RootElement.Clone()
        };
    }

    private string Versioned(string path)
    {
        return $"{path}?v={_cacheToken}";
    }

    private async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using Stream stream = await httpClient.GetStreamAsync(path, cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidDataException($"El archivo {path} está vacío.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new DateOnlyConverter());
        return options;
    }

    private sealed class DateOnlyConverter : JsonConverter<DateOnly>
    {
        public override DateOnly Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return DateOnly.Parse(
                reader.GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateOnly value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
