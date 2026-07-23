using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Persistence;

public sealed class JsonPromotionStateRepository(
    ILogger<JsonPromotionStateRepository>? logger = null) : IPromotionStateRepository
{
    private const string StateFileName = "promotions-state.json";
    private const string BackupOneFileName = "promotions-state.backup-1.json";
    private const string BackupTwoFileName = "promotions-state.backup-2.json";
    private static readonly Action<ILogger, string, Exception?> LogUnreadableState =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3001, "UnreadablePromotionState"),
            "No se pudo leer el estado {StatePath}; se intentará la siguiente copia.");
    private readonly ILogger<JsonPromotionStateRepository> _logger =
        logger ?? NullLogger<JsonPromotionStateRepository>.Instance;

    public async Task<IReadOnlyList<Promotion>> LoadAsync(
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        string[] candidates =
        [
            Path.Combine(stateDirectory, StateFileName),
            Path.Combine(stateDirectory, BackupOneFileName),
            Path.Combine(stateDirectory, BackupTwoFileName)
        ];
        List<Exception> failures = [];
        foreach (string path in candidates.Where(File.Exists))
        {
            try
            {
                await using FileStream stream = File.OpenRead(path);
                Promotion[]? promotions = await JsonSerializer.DeserializeAsync<Promotion[]>(
                    stream,
                    JsonDefaults.Compact,
                    cancellationToken);
                if (promotions is null)
                {
                    throw new InvalidDataException(
                        $"El estado '{path}' contiene null en lugar de una colección.");
                }

                return promotions;
            }
            catch (Exception exception) when (
                exception is JsonException or InvalidDataException or IOException)
            {
                failures.Add(exception);
                LogUnreadableState(_logger, path, exception);
            }
        }

        if (failures.Count == 0)
        {
            return [];
        }

        throw new InvalidDataException(
            "El estado principal y todas sus copias de seguridad están dañados o no se pueden leer.",
            new AggregateException(failures));
    }

    public async Task SaveAsync(
        string stateDirectory,
        IReadOnlyList<Promotion> promotions,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        string path = Path.Combine(stateDirectory, StateFileName);
        await RotateBackupsAsync(stateDirectory, path, cancellationToken);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(
                    promotions.OrderBy(item => item.Id, StringComparer.Ordinal),
                    JsonDefaults.Indented),
                cancellationToken);
            await AtomicFile.ReplaceAsync(temporary, path, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }

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

    private static async Task RotateBackupsAsync(
        string stateDirectory,
        string currentPath,
        CancellationToken cancellationToken)
    {
        string backupOne = Path.Combine(stateDirectory, BackupOneFileName);
        string backupTwo = Path.Combine(stateDirectory, BackupTwoFileName);
        await CopyAtomicallyIfExistsAsync(backupOne, backupTwo, cancellationToken);
        await CopyAtomicallyIfExistsAsync(currentPath, backupOne, cancellationToken);
    }

    private static async Task CopyAtomicallyIfExistsAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(source))
        {
            return;
        }

        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream input = File.OpenRead(source))
            await using (FileStream output = new(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            await AtomicFile.ReplaceAsync(temporary, destination, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
