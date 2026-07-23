using System.Globalization;
using System.Text;
using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Persistence;

public sealed class PublicDataWriter : IPublicDataWriter
{
    public async Task WriteAsync(
        string outputDirectory,
        PromotionDataset dataset,
        ChangeDataset changes,
        RunReport run,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        ChangeDataset mergedChanges = MergePreviousChanges(outputDirectory, changes);
        List<(string Temporary, string Destination)> staged = [];
        try
        {
            staged.Add(await StageTextAsync(
                outputDirectory,
                "promotions.json",
                JsonSerializer.Serialize(dataset, JsonDefaults.Indented),
                cancellationToken));
            staged.Add(await StageTextAsync(
                outputDirectory,
                "changes.json",
                JsonSerializer.Serialize(mergedChanges, JsonDefaults.Indented),
                cancellationToken));
            staged.Add(await StageTextAsync(
                outputDirectory,
                "run.json",
                JsonSerializer.Serialize(run, JsonDefaults.Indented),
                cancellationToken));
            staged.Add(await StageTextAsync(
                outputDirectory,
                "promotions.geojson",
                BuildGeoJson(dataset.Promotions),
                cancellationToken));
            staged.Add(await StageTextAsync(
                outputDirectory,
                "promotions.csv",
                BuildCsv(dataset.Promotions),
                cancellationToken,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)));

            foreach ((string temporary, string destination) in staged)
            {
                await AtomicFile.ReplaceAsync(
                    temporary,
                    destination,
                    cancellationToken);
            }
        }
        finally
        {
            foreach ((string temporary, _) in staged.Where(item => File.Exists(item.Temporary)))
            {
                File.Delete(temporary);
            }
        }
    }

    private static ChangeDataset MergePreviousChanges(
        string outputDirectory,
        ChangeDataset current)
    {
        string path = Path.Combine(outputDirectory, "changes.json");
        if (!File.Exists(path))
        {
            return current;
        }

        try
        {
            ChangeDataset? previous = JsonSerializer.Deserialize<ChangeDataset>(
                File.ReadAllText(path),
                JsonDefaults.Compact);
            if (previous is null)
            {
                return current;
            }

            return new()
            {
                RunId = current.RunId,
                GeneratedAtUtc = current.GeneratedAtUtc,
                Changes = current.Changes.Concat(previous.Changes)
                    .DistinctBy(change => change.Id, StringComparer.Ordinal)
                    .OrderByDescending(change => change.DetectedAtUtc)
                    .Take(5_000)
                    .ToArray()
            };
        }
        catch (JsonException)
        {
            return current;
        }
    }

    public static string BuildCsv(IReadOnlyList<Promotion> promotions)
    {
        string[] columns =
        [
            "id",
            "name",
            "municipality",
            "locality",
            "propertyTypes",
            "commercialStatus",
            "constructionStatus",
            "priceFrom",
            "priceTo",
            "bedroomsMin",
            "bedroomsMax",
            "builtAreaMinSqm",
            "builtAreaMaxSqm",
            "plotAreaMinSqm",
            "plotAreaMaxSqm",
            "developerName",
            "sourceKind",
            "sourceConfidence",
            "latitude",
            "longitude",
            "locationPrecision",
            "active",
            "lastSeenUtc",
            "canonicalUrl"
        ];
        StringBuilder csv = new();
        csv.AppendLine(string.Join(',', columns));
        foreach (Promotion promotion in promotions.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            string?[] values =
            [
                promotion.Id,
                promotion.Name,
                promotion.Municipality,
                promotion.Locality,
                string.Join(" | ", promotion.PropertyTypes),
                promotion.CommercialStatus.ToString(),
                promotion.ConstructionStatus.ToString(),
                Format(promotion.PriceFrom),
                Format(promotion.PriceTo),
                Format(promotion.BedroomsMin),
                Format(promotion.BedroomsMax),
                Format(promotion.BuiltAreaMinSqm),
                Format(promotion.BuiltAreaMaxSqm),
                Format(promotion.PlotAreaMinSqm),
                Format(promotion.PlotAreaMaxSqm),
                promotion.DeveloperName,
                promotion.SourceKind.ToString(),
                Format(promotion.SourceConfidence),
                Format(promotion.Latitude),
                Format(promotion.Longitude),
                promotion.LocationPrecision.ToString(),
                promotion.Active ? "true" : "false",
                promotion.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture),
                promotion.CanonicalUrl
            ];
            csv.AppendLine(string.Join(',', values.Select(EscapeCsv)));
        }

        return csv.ToString();
    }

    public static string BuildGeoJson(IReadOnlyList<Promotion> promotions)
    {
        object geoJson = new
        {
            type = "FeatureCollection",
            features = promotions
                .Where(item => item.Latitude.HasValue && item.Longitude.HasValue)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new
                {
                    type = "Feature",
                    id = item.Id,
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[] { item.Longitude!.Value, item.Latitude!.Value }
                    },
                    properties = new
                    {
                        item.Name,
                        item.Municipality,
                        item.PriceFrom,
                        item.CommercialStatus,
                        item.LocationPrecision,
                        item.CanonicalUrl
                    }
                })
                .ToArray()
        };
        return JsonSerializer.Serialize(geoJson, JsonDefaults.Indented);
    }

    private static async Task<(string Temporary, string Destination)> StageTextAsync(
        string directory,
        string filename,
        string content,
        CancellationToken cancellationToken,
        Encoding? encoding = null)
    {
        string destination = Path.Combine(directory, filename);
        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporary,
            content,
            encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return (temporary, destination);
    }

    private static string EscapeCsv(string? value)
    {
        string text = value ?? string.Empty;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;
    }

    private static string? Format(object? value)
    {
        return value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }
}
