using System.Text.Json;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Persistence;

public sealed class JsonEnrichmentStateRepository : IEnrichmentStateRepository
{
    public const string FileName = "promotion-enrichment.json";

    public async Task<EnrichmentState> LoadAsync(
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(stateDirectory, FileName);
        if (!File.Exists(path))
        {
            return new();
        }

        await using FileStream stream = File.OpenRead(path);
        EnrichmentState? queue = await JsonSerializer.DeserializeAsync<EnrichmentState>(
            stream,
            JsonDefaults.Compact,
            cancellationToken);
        return queue ?? throw new InvalidDataException(
            $"El estado privado de enriquecimiento '{path}' contiene null.");
    }

    public async Task SaveAsync(
        string stateDirectory,
        EnrichmentState queue,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        string path = Path.Combine(stateDirectory, FileName);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        EnrichmentState stable = new()
        {
            GeneratedAtUtc = queue.GeneratedAtUtc,
            Items = queue.Items
                .OrderBy(item => item.PromotionId, StringComparer.Ordinal)
                .ThenBy(item => item.GeneratedAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            Runs = queue.Runs
                .OrderBy(run => run.StartedAtUtc)
                .ThenBy(run => run.Id, StringComparer.Ordinal)
                .TakeLast(100)
                .ToArray()
        };

        try
        {
            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(stable, JsonDefaults.Indented),
                cancellationToken);
            await AtomicFile.ReplaceAsync(temporary, path, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
