using System.Text.Json;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Persistence;

public sealed class JsonOpportunityReportWriter
{
    public const string AuditFileName = "opportunity-audit.json";

    public const string BackfillFileName = "opportunity-backfill.json";

    public const string TriageFileName = "opportunity-triage.json";

    public Task SaveAuditAsync(
        string stateDirectory,
        OpportunityAuditReport report,
        CancellationToken cancellationToken)
    {
        return SaveAsync(
            Path.Combine(stateDirectory, AuditFileName),
            report,
            cancellationToken);
    }

    public Task SaveBackfillAsync(
        string stateDirectory,
        OpportunityBackfillReport report,
        CancellationToken cancellationToken)
    {
        return SaveAsync(
            Path.Combine(stateDirectory, BackfillFileName),
            report,
            cancellationToken);
    }

    public Task SaveTriageAsync(
        string stateDirectory,
        OpportunityTriageReport report,
        CancellationToken cancellationToken)
    {
        return SaveAsync(
            Path.Combine(stateDirectory, TriageFileName),
            report,
            cancellationToken);
    }

    private static async Task SaveAsync<T>(
        string destination,
        T report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            string json = JsonSerializer.Serialize(report, JsonDefaults.Indented) + "\n";
            await File.WriteAllTextAsync(temporary, json, cancellationToken);
            await AtomicFile.ReplaceAsync(temporary, destination, cancellationToken);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
