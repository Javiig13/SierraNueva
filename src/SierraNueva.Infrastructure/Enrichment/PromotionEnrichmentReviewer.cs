using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;

namespace SierraNueva.Infrastructure.Enrichment;

public sealed class PromotionEnrichmentReviewer(
    IEnrichmentStateRepository stateRepository,
    IClock clock)
{
    public async Task<PromotionEnrichment> ReviewAsync(
        string stateDirectory,
        string proposalId,
        string? fieldName,
        EnrichmentReviewStatus decision,
        CancellationToken cancellationToken)
    {
        if (decision is not (EnrichmentReviewStatus.Accepted or EnrichmentReviewStatus.Rejected))
        {
            throw new ArgumentException("La revisión solo admite accepted o rejected.");
        }

        EnrichmentState queue = await stateRepository.LoadAsync(stateDirectory, cancellationToken);
        PromotionEnrichment selected = queue.Items.SingleOrDefault(item => item.Id == proposalId) ??
            throw new InvalidDataException($"No existe la propuesta '{proposalId}'.");
        if (selected.Status == EnrichmentReviewStatus.Stale)
        {
            throw new InvalidDataException(
                $"La propuesta '{proposalId}' está obsoleta y no se puede revisar.");
        }

        EnrichmentFieldProposal[] pending = selected.Fields
            .Where(field => field.Status == EnrichmentReviewStatus.Pending)
            .ToArray();
        if (fieldName is null && pending.Length != 1)
        {
            throw new InvalidDataException(
                pending.Length == 0
                    ? $"La propuesta '{proposalId}' no tiene campos pendientes."
                    : "La propuesta contiene varios campos; indica --field para revisar " +
                      "solo uno.");
        }

        string selectedFieldName = fieldName ?? pending[0].Field;
        EnrichmentFieldProposal selectedField = selected.Fields.SingleOrDefault(
            field => field.Field == selectedFieldName) ??
            throw new InvalidDataException(
                $"La propuesta '{proposalId}' no contiene el campo '{selectedFieldName}'.");
        DateTimeOffset reviewedAt = clock.UtcNow;
        EnrichmentFieldProposal[] reviewedFields = selected.Fields
            .Select(field => field.Field == selectedField.Field
                ? CopyFieldWithStatus(field, decision, reviewedAt)
                : field)
            .ToArray();
        EnrichmentReviewStatus status = reviewedFields.Any(
            field => field.Status == EnrichmentReviewStatus.Pending)
            ? EnrichmentReviewStatus.Pending
            : reviewedFields.Any(field => field.Status == EnrichmentReviewStatus.Accepted)
                ? EnrichmentReviewStatus.Accepted
                : EnrichmentReviewStatus.Rejected;
        PromotionEnrichment reviewed = CopyWithFields(
            selected,
            reviewedFields,
            status,
            reviewedAt);
        await stateRepository.SaveAsync(
            stateDirectory,
            new()
            {
                GeneratedAtUtc = clock.UtcNow,
                Items = queue.Items.Select(item => item.Id == proposalId ? reviewed : item).ToArray(),
                Runs = queue.Runs
            },
            cancellationToken);
        return reviewed;
    }

    private static PromotionEnrichment CopyWithFields(
        PromotionEnrichment source,
        IReadOnlyList<EnrichmentFieldProposal> fields,
        EnrichmentReviewStatus status,
        DateTimeOffset reviewedAt)
    {
        return new()
        {
            Id = source.Id,
            PromotionId = source.PromotionId,
            PromotionName = source.PromotionName,
            CanonicalUrl = source.CanonicalUrl,
            ContentHash = source.ContentHash,
            Provider = source.Provider,
            Model = source.Model,
            EvidenceFetchedAtUtc = source.EvidenceFetchedAtUtc,
            GeneratedAtUtc = source.GeneratedAtUtc,
            Status = status,
            ReviewedAtUtc = reviewedAt,
            Usage = source.Usage,
            MaximumCostEstimateUsd = source.MaximumCostEstimateUsd,
            Fields = fields,
            Warnings = source.Warnings
        };
    }

    private static EnrichmentFieldProposal CopyFieldWithStatus(
        EnrichmentFieldProposal source,
        EnrichmentReviewStatus status,
        DateTimeOffset reviewedAt)
    {
        return new()
        {
            Field = source.Field,
            ValueText = source.ValueText,
            SourceUrl = source.SourceUrl,
            EvidenceText = source.EvidenceText,
            Confidence = source.Confidence,
            Status = status,
            ReviewedAtUtc = reviewedAt
        };
    }

    internal static PromotionEnrichment CopyWithStatus(
        PromotionEnrichment source,
        EnrichmentReviewStatus status,
        DateTimeOffset reviewedAt)
    {
        return new()
        {
            Id = source.Id,
            PromotionId = source.PromotionId,
            PromotionName = source.PromotionName,
            CanonicalUrl = source.CanonicalUrl,
            ContentHash = source.ContentHash,
            Provider = source.Provider,
            Model = source.Model,
            EvidenceFetchedAtUtc = source.EvidenceFetchedAtUtc,
            GeneratedAtUtc = source.GeneratedAtUtc,
            Status = status,
            ReviewedAtUtc = reviewedAt,
            Usage = source.Usage,
            MaximumCostEstimateUsd = source.MaximumCostEstimateUsd,
            Fields = source.Fields,
            Warnings = source.Warnings
        };
    }
}
