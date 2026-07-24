using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Enrichment;
using SierraNueva.Core.Models;

namespace SierraNueva.Infrastructure.Enrichment;

public sealed partial class PromotionEnrichmentRunner(
    IPageSource pageSource,
    IEnrichmentStateRepository stateRepository,
    IPromotionEnrichmentProvider provider,
    IClock clock)
{
    public async Task<EnrichmentRunResult> RunAsync(
        IReadOnlyList<Promotion> promotions,
        IReadOnlyList<SourceDefinition> sources,
        CrawlerSettings settings,
        string stateDirectory,
        string? promotionFilter,
        int maxPromotions,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        EnrichmentState previous = await stateRepository.LoadAsync(
            stateDirectory,
            cancellationToken);
        List<PromotionEnrichment> items = [.. previous.Items];
        Promotion[] eligible = promotions
            .Where(promotion => promotion.Active)
            .Where(promotion => promotionFilter is null ||
                                promotion.Id.Equals(
                                    promotionFilter,
                                    StringComparison.OrdinalIgnoreCase))
            .Where(promotion => PromotionEnrichmentPolicy.MissingFields(promotion).Count > 0)
            .OrderBy(promotion => Completeness(promotion))
            .ThenBy(promotion => promotion.Id, StringComparer.Ordinal)
            .Take(maxPromotions)
            .ToArray();

        int processed = 0;
        int cached = 0;
        int proposed = 0;
        int failed = 0;
        foreach (Promotion promotion in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SourceDefinition source = FindSource(promotion, sources);
                PageBatch batch = await pageSource.FetchAsync(
                    CreateEvidenceSource(promotion, source),
                    settings,
                    4,
                    disablePlaywright: true,
                    cancellationToken);
                EnrichmentEvidenceDocument evidence = BuildEvidence(promotion, batch.Pages);
                PromotionEnrichment? current = items
                    .Where(item => item.PromotionId == promotion.Id)
                    .OrderByDescending(item => item.GeneratedAtUtc)
                    .FirstOrDefault();
                if (current is not null && current.ContentHash == evidence.ContentHash)
                {
                    cached++;
                    continue;
                }

                if (current is not null && current.Status == EnrichmentReviewStatus.Accepted)
                {
                    int index = items.IndexOf(current);
                    items[index] = PromotionEnrichmentReviewer.CopyWithStatus(
                        current,
                        EnrichmentReviewStatus.Stale,
                        clock.UtcNow);
                }

                IReadOnlyList<string> missing = PromotionEnrichmentPolicy.MissingFields(promotion);
                IReadOnlyList<EnrichmentFieldProposal> raw = await provider.ProposeAsync(
                    evidence,
                    missing,
                    cancellationToken);
                IReadOnlyList<EnrichmentFieldProposal> fields =
                    PromotionEnrichmentPolicy.Validate(
                        evidence,
                        missing,
                        raw,
                        0.8m,
                        out IReadOnlyList<string> warnings);
                string id = $"enr-{promotion.Id}-{evidence.ContentHash[..12].ToLowerInvariant()}";
                items.RemoveAll(item => item.Id == id);
                items.Add(new()
                {
                    Id = id,
                    PromotionId = promotion.Id,
                    PromotionName = promotion.Name,
                    CanonicalUrl = promotion.CanonicalUrl,
                    ContentHash = evidence.ContentHash,
                    Provider = provider.ProviderName,
                    Model = provider.Model,
                    EvidenceFetchedAtUtc = evidence.FetchedAtUtc,
                    GeneratedAtUtc = clock.UtcNow,
                    Fields = fields,
                    Warnings = warnings
                });
                processed++;
                proposed += fields.Count;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                items.Add(new()
                {
                    Id = $"enr-error-{promotion.Id}-{clock.UtcNow:yyyyMMddHHmmss}",
                    PromotionId = promotion.Id,
                    PromotionName = promotion.Name,
                    CanonicalUrl = promotion.CanonicalUrl,
                    Provider = provider.ProviderName,
                    Model = provider.Model,
                    EvidenceFetchedAtUtc = clock.UtcNow,
                    GeneratedAtUtc = clock.UtcNow,
                    Warnings = [exception.Message]
                });
            }
        }

        if (!dryRun)
        {
            await stateRepository.SaveAsync(
                stateDirectory,
                new()
                {
                    GeneratedAtUtc = clock.UtcNow,
                    Items = items
                },
                cancellationToken);
        }

        return new()
        {
            EligiblePromotions = eligible.Length,
            ProcessedPromotions = processed,
            CachedPromotions = cached,
            ProposedFields = proposed,
            FailedPromotions = failed,
            DryRun = dryRun
        };
    }

    internal static EnrichmentEvidenceDocument BuildEvidence(
        Promotion promotion,
        IReadOnlyList<FetchedPage> pages)
    {
        HtmlParser parser = new();
        List<EnrichmentEvidencePage> evidencePages = [];
        int remaining = 24_000;
        foreach (FetchedPage page in pages.OrderBy(item => item.Url.AbsoluteUri, StringComparer.Ordinal))
        {
            if (remaining <= 0)
            {
                break;
            }

            string text = page.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase)
                ? parser.ParseDocument(page.Content).Body?.TextContent ?? string.Empty
                : page.Content;
            text = Whitespace().Replace(text, " ").Trim();
            if (text.Length == 0)
            {
                continue;
            }

            int length = Math.Min(Math.Min(text.Length, 8_000), remaining);
            evidencePages.Add(new()
            {
                Url = page.Url.AbsoluteUri,
                Text = text[..length]
            });
            remaining -= length;
        }

        if (evidencePages.Count == 0)
        {
            throw new InvalidDataException(
                $"No se obtuvo evidencia textual para '{promotion.Name}'.");
        }

        string canonical = string.Join(
            "\n",
            evidencePages.Select(page => $"{page.Url}\n{page.Text}"));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new()
        {
            PromotionId = promotion.Id,
            PromotionName = promotion.Name,
            Municipality = promotion.Municipality,
            CanonicalUrl = promotion.CanonicalUrl,
            ContentHash = hash,
            FetchedAtUtc = pages.Max(page => page.FetchedAtUtc),
            Pages = evidencePages
        };
    }

    private static SourceDefinition FindSource(
        Promotion promotion,
        IReadOnlyList<SourceDefinition> sources)
    {
        Uri canonical = new(promotion.CanonicalUrl);
        return sources.FirstOrDefault(source =>
                   source.AllowedHosts.Contains(canonical.Host, StringComparer.OrdinalIgnoreCase) ||
                   Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out Uri? sourceUri) &&
                   sourceUri.Host.Equals(canonical.Host, StringComparison.OrdinalIgnoreCase)) ??
               throw new InvalidDataException(
                   $"No hay una fuente configurada para '{promotion.CanonicalUrl}'.");
    }

    private static SourceDefinition CreateEvidenceSource(
        Promotion promotion,
        SourceDefinition source)
    {
        return new()
        {
            Id = $"enrichment-{source.Id}",
            Name = $"Evidencia para {promotion.Name}",
            BaseUrl = source.BaseUrl,
            Enabled = true,
            SourceKind = source.SourceKind,
            AllowedHosts = source.AllowedHosts,
            StartUrls = [promotion.CanonicalUrl],
            UseRobots = source.UseRobots,
            UseSitemaps = false,
            FollowInternalLinks = true,
            MaxDepth = 1,
            MaxPages = 4,
            RequestDelayMilliseconds = source.RequestDelayMilliseconds,
            UsePlaywright = false,
            FixedMunicipality = promotion.Municipality,
            MunicipalityHints = source.MunicipalityHints,
            ContentSelector = source.ContentSelector,
            AdditionalContentSelectors = source.AdditionalContentSelectors,
            IncludePatterns = source.IncludePatterns,
            ExcludePatterns = source.ExcludePatterns,
            FixturePath = source.FixturePath,
            Notes = "Recorrido acotado de evidencia privada para enriquecimiento."
        };
    }

    private static int Completeness(Promotion promotion)
    {
        return 24 - PromotionEnrichmentPolicy.MissingFields(promotion).Count;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
