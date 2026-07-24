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
        EnrichmentRunOptions options,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startedAtUtc = clock.UtcNow;
        EnrichmentState previous = await stateRepository.LoadAsync(
            stateDirectory,
            cancellationToken);
        List<PromotionEnrichment> items = [.. previous.Items];
        Promotion[] eligible = promotions
            .Where(promotion => promotion.Active)
            .Where(promotion => options.PromotionFilter is null ||
                                promotion.Id.Equals(
                                    options.PromotionFilter,
                                    StringComparison.OrdinalIgnoreCase))
            .Where(promotion => PromotionEnrichmentPolicy.MissingFields(promotion).Count > 0)
            .OrderBy(promotion => Completeness(promotion))
            .ThenBy(promotion => promotion.Id, StringComparer.Ordinal)
            .ToArray();

        int processed = 0;
        int cached = 0;
        int planned = 0;
        int budgetSkipped = 0;
        int proposed = 0;
        int failed = 0;
        int providerCalls = 0;
        decimal reservedMaximumCost = 0;
        decimal accountedCost = 0;
        EnrichmentUsage usage = new();
        foreach (Promotion promotion in eligible)
        {
            if (providerCalls + planned >= options.MaxPromotions)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<string> missing = PromotionEnrichmentPolicy.MissingFields(promotion);
                SourceDefinition source = FindSource(promotion, sources);
                PageBatch batch = await pageSource.FetchAsync(
                    CreateEvidenceSource(promotion, source, options.MaxEvidencePages),
                    settings,
                    options.MaxEvidencePages,
                    disablePlaywright: true,
                    cancellationToken);
                EnrichmentEvidenceDocument evidence = BuildEvidence(
                    promotion,
                    batch.Pages,
                    missing,
                    options.MaxEvidenceCharacters);
                PromotionEnrichment? current = items
                    .Where(item => item.PromotionId == promotion.Id)
                    .OrderByDescending(item => item.GeneratedAtUtc)
                    .FirstOrDefault();
                if (current is not null && current.ContentHash == evidence.ContentHash)
                {
                    cached++;
                    continue;
                }

                EnrichmentCostEstimate estimate = provider.EstimateMaximumCost(
                    evidence,
                    missing);
                if (accountedCost + estimate.MaximumCostUsd > options.MaxCostUsd)
                {
                    budgetSkipped++;
                    continue;
                }

                if (options.DryRun)
                {
                    planned++;
                    reservedMaximumCost += estimate.MaximumCostUsd;
                    accountedCost += estimate.MaximumCostUsd;
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

                providerCalls++;
                reservedMaximumCost += estimate.MaximumCostUsd;
                EnrichmentProviderResult response = await provider.ProposeAsync(
                    evidence,
                    missing,
                    cancellationToken);
                EnrichmentUsage responseUsage = response.Usage;
                usage = AddUsage(usage, responseUsage);
                accountedCost += responseUsage.TotalTokens == 0
                    ? estimate.MaximumCostUsd
                    : responseUsage.EstimatedCostUsd;
                IReadOnlyList<EnrichmentFieldProposal> fields =
                    PromotionEnrichmentPolicy.Validate(
                        evidence,
                        missing,
                        response.Fields,
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
                    Usage = responseUsage,
                    MaximumCostEstimateUsd = estimate.MaximumCostUsd,
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

        if (!options.DryRun)
        {
            EnrichmentRunAudit run = new()
            {
                Id = $"enrichment-{startedAtUtc:yyyyMMddTHHmmssfffZ}",
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = clock.UtcNow,
                Provider = provider.ProviderName,
                Model = provider.Model,
                MaxPromotions = options.MaxPromotions,
                MaxEvidencePages = options.MaxEvidencePages,
                MaxEvidenceCharacters = options.MaxEvidenceCharacters,
                MaxOutputTokens = provider.MaxOutputTokens,
                MaxCostUsd = options.MaxCostUsd,
                ProcessedPromotions = processed,
                CachedPromotions = cached,
                FailedPromotions = failed,
                BudgetSkippedPromotions = budgetSkipped,
                Usage = usage
            };
            await stateRepository.SaveAsync(
                stateDirectory,
                new()
                {
                    GeneratedAtUtc = clock.UtcNow,
                    Items = items,
                    Runs = [.. previous.Runs, run]
                },
                cancellationToken);
        }

        return new()
        {
            EligiblePromotions = eligible.Length,
            ProcessedPromotions = processed,
            CachedPromotions = cached,
            PlannedPromotions = planned,
            BudgetSkippedPromotions = budgetSkipped,
            ProposedFields = proposed,
            FailedPromotions = failed,
            ReservedMaximumCostUsd = reservedMaximumCost,
            Usage = usage,
            DryRun = options.DryRun
        };
    }

    internal static EnrichmentEvidenceDocument BuildEvidence(
        Promotion promotion,
        IReadOnlyList<FetchedPage> pages,
        IReadOnlyList<string> missingFields,
        int maxCharacters)
    {
        HtmlParser parser = new();
        List<EnrichmentEvidencePage> evidencePages = [];
        int remaining = maxCharacters;
        IReadOnlyList<string> keywords = EvidenceKeywords(promotion, missingFields);
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

            int length = Math.Min(remaining, 3_500);
            string selected = SelectRelevantText(text, keywords, length);
            evidencePages.Add(new()
            {
                Url = page.Url.AbsoluteUri,
                Text = selected
            });
            remaining -= selected.Length;
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
        SourceDefinition source,
        int maxPages)
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
            RequireResponseBody = true,
            MaxDepth = 1,
            MaxPages = maxPages,
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

    private static EnrichmentUsage AddUsage(
        EnrichmentUsage left,
        EnrichmentUsage right)
    {
        return new()
        {
            InputTokens = left.InputTokens + right.InputTokens,
            CachedInputTokens = left.CachedInputTokens + right.CachedInputTokens,
            CacheWriteTokens = left.CacheWriteTokens + right.CacheWriteTokens,
            OutputTokens = left.OutputTokens + right.OutputTokens,
            ReasoningTokens = left.ReasoningTokens + right.ReasoningTokens,
            TotalTokens = left.TotalTokens + right.TotalTokens,
            EstimatedCostUsd = left.EstimatedCostUsd + right.EstimatedCostUsd
        };
    }

    private static IReadOnlyList<string> EvidenceKeywords(
        Promotion promotion,
        IReadOnlyList<string> missingFields)
    {
        HashSet<string> keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            promotion.Name,
            promotion.Municipality
        };
        foreach (string field in missingFields)
        {
            IReadOnlyList<string> fieldKeywords = field switch
            {
                "priceFrom" or "priceTo" =>
                    ["precio", "desde", "hasta", "euros", "€"],
                "bedroomsMin" or "bedroomsMax" =>
                    ["dormitorio", "habitación", "habitacion"],
                "bathroomsMin" or "bathroomsMax" =>
                    ["baño", "bano", "aseo"],
                "usableAreaMinSqm" or "usableAreaMaxSqm" =>
                    ["útiles", "utiles", "superficie", "m²", "m2"],
                "builtAreaMinSqm" or "builtAreaMaxSqm" =>
                    ["construidos", "construida", "superficie", "m²", "m2"],
                "plotAreaMinSqm" or "plotAreaMaxSqm" =>
                    ["parcela", "jardín", "jardin", "m²", "m2"],
                "developerName" or "marketerName" or "cooperativeName" =>
                    ["promotora", "promueve", "comercializa", "cooperativa", "developer"],
                "totalUnits" or "availableUnits" =>
                    ["viviendas", "unidades", "disponibles"],
                "garageSpacesMin" or "garageSpacesMax" =>
                    ["garaje", "aparcamiento", "plaza"],
                "deliveryDateText" =>
                    ["entrega", "finalización", "finalizacion", "llaves"],
                "buildingLicenceStatus" =>
                    ["licencia", "concedida", "obtenida", "solicitada"],
                "address" or "postalCode" =>
                    ["dirección", "direccion", "calle", "avenida", "código postal"],
                _ => Array.Empty<string>()
            };
            foreach (string keyword in fieldKeywords)
            {
                keywords.Add(keyword);
            }
        }

        return [.. keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword))];
    }

    private static string SelectRelevantText(
        string text,
        IReadOnlyList<string> keywords,
        int maximumLength)
    {
        if (text.Length <= maximumLength)
        {
            return text;
        }

        const int chunkLength = 520;
        const int chunkStep = 440;
        List<(int Start, int Score, string Text)> chunks = [];
        for (int start = 0; start < text.Length; start += chunkStep)
        {
            int length = Math.Min(chunkLength, text.Length - start);
            string chunk = text.Substring(start, length).Trim();
            if (chunk.Length == 0)
            {
                continue;
            }

            int score = keywords.Count(keyword =>
                chunk.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            chunks.Add((start, score, chunk));
        }

        List<(int Start, int Score, string Text)> selected = [];
        int used = 0;
        foreach ((int Start, int Score, string Text) chunk in chunks
                     .OrderByDescending(chunk => chunk.Score)
                     .ThenBy(chunk => chunk.Start))
        {
            int separatorLength = selected.Count == 0 ? 0 : 3;
            if (used + separatorLength >= maximumLength)
            {
                break;
            }

            int available = maximumLength - used - separatorLength;
            string value = chunk.Text[..Math.Min(chunk.Text.Length, available)];
            selected.Add((chunk.Start, chunk.Score, value));
            used += separatorLength + value.Length;
        }

        return string.Join(
            " … ",
            selected.OrderBy(chunk => chunk.Start).Select(chunk => chunk.Text));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
