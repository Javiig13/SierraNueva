using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Persistence;

public sealed class JsonPromotionStateRepository : IPromotionStateRepository
{
    public async Task<IReadOnlyList<Promotion>> LoadAsync(
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(stateDirectory, "promotions-state.json");
        if (!File.Exists(path))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Promotion[]>(
                   stream,
                   JsonDefaults.Compact,
                   cancellationToken)
               ?? [];
    }

    public async Task SaveAsync(
        string stateDirectory,
        IReadOnlyList<Promotion> promotions,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        string path = Path.Combine(stateDirectory, "promotions-state.json");
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(
            temporary,
            JsonSerializer.Serialize(
                promotions.OrderBy(item => item.Id, StringComparer.Ordinal),
                JsonDefaults.Indented),
            cancellationToken);
        await AtomicFile.ReplaceAsync(temporary, path, cancellationToken);

        string geocodingCache = Path.Combine(stateDirectory, "geocoding-cache.json");
        if (!File.Exists(geocodingCache))
        {
            await File.WriteAllTextAsync(geocodingCache, "{}\n", cancellationToken);
        }

        string httpCache = Path.Combine(stateDirectory, "http-cache.json");
        if (!File.Exists(httpCache))
        {
            await File.WriteAllTextAsync(httpCache, "{}\n", cancellationToken);
        }
    }
}
