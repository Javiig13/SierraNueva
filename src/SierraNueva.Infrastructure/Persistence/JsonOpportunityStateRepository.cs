using System.Text.Json;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Persistence;

public sealed class JsonOpportunityStateRepository : IOpportunityStateRepository
{
    private const string StateFileName = "opportunity-candidates.json";
    private const string BackupOneFileName = "opportunity-candidates.backup-1.json";
    private const string BackupTwoFileName = "opportunity-candidates.backup-2.json";

    public async Task<OpportunityRadarState> LoadAsync(
        string stateDirectory,
        CancellationToken cancellationToken)
    {
        string[] paths =
        [
            Path.Combine(stateDirectory, StateFileName),
            Path.Combine(stateDirectory, BackupOneFileName),
            Path.Combine(stateDirectory, BackupTwoFileName)
        ];
        List<Exception> failures = [];
        foreach (string path in paths.Where(File.Exists))
        {
            try
            {
                await using FileStream stream = File.OpenRead(path);
                OpportunityRadarState? state =
                    await JsonSerializer.DeserializeAsync<OpportunityRadarState>(
                        stream,
                        JsonDefaults.Compact,
                        cancellationToken);
                if (state is null || state.SchemaVersion != "1.0")
                {
                    throw new InvalidDataException(
                        $"El estado de oportunidades '{path}' no respeta el contrato 1.0.");
                }

                return state;
            }
            catch (Exception exception) when (
                exception is JsonException or InvalidDataException or IOException)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidDataException(
                "El estado de oportunidades y todas sus copias están dañados.",
                new AggregateException(failures));
        }

        return new();
    }

    public async Task SaveAsync(
        string stateDirectory,
        OpportunityRadarState state,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stateDirectory);
        string destination = Path.Combine(stateDirectory, StateFileName);
        await CopyIfExistsAsync(
            Path.Combine(stateDirectory, BackupOneFileName),
            Path.Combine(stateDirectory, BackupTwoFileName),
            cancellationToken);
        await CopyIfExistsAsync(
            destination,
            Path.Combine(stateDirectory, BackupOneFileName),
            cancellationToken);

        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            string json = JsonSerializer.Serialize(state, JsonDefaults.Indented) + "\n";
            await File.WriteAllTextAsync(temporary, json, cancellationToken);
            await AtomicFile.ReplaceAsync(temporary, destination, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static async Task CopyIfExistsAsync(
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
            File.Copy(source, temporary, overwrite: true);
            await AtomicFile.ReplaceAsync(temporary, destination, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
