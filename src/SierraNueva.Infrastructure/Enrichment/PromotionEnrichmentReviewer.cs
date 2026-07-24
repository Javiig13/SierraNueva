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
        PromotionEnrichment reviewed = CopyWithStatus(selected, decision, clock.UtcNow);
        await stateRepository.SaveAsync(
            stateDirectory,
            new()
            {
                GeneratedAtUtc = clock.UtcNow,
                Items = queue.Items.Select(item => item.Id == proposalId ? reviewed : item).ToArray()
            },
            cancellationToken);
        return reviewed;
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
            Fields = source.Fields,
            Warnings = source.Warnings
        };
    }
}
