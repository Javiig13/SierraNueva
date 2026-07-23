using System.Text.Json;
using System.Text.Json.Serialization;
using SierraNueva.Contracts;
using SierraNueva.Web.Models;

namespace SierraNueva.Web.Services;

public sealed class PublicDataService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public async Task<SiteData> LoadAsync(CancellationToken cancellationToken)
    {
        PromotionDataset promotions = await LoadAsync<PromotionDataset>(
            "data/promotions.json",
            cancellationToken);
        ChangeDataset changes = await LoadAsync<ChangeDataset>(
            "data/changes.json",
            cancellationToken);
        RunReport run = await LoadAsync<RunReport>("data/run.json", cancellationToken);
        using JsonDocument geoJson = JsonDocument.Parse(
            await httpClient.GetStringAsync("data/promotions.geojson", cancellationToken));
        return new()
        {
            Promotions = promotions,
            Changes = changes,
            Run = run,
            GeoJson = geoJson.RootElement.Clone()
        };
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
