using System.Reflection;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Changes;
using SierraNueva.Core.Identity;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;
using SierraNueva.Core.Quality;

namespace SierraNueva.Core.Crawling;

public sealed class CrawlPipeline(
    IPageSource pageSource,
    IPromotionExtractor extractor,
    IGeocoder geocoder,
    IPromotionStateRepository stateRepository,
    IPublicDataWriter publicDataWriter,
    IClock clock)
{
    private readonly PromotionDeduplicator _deduplicator = new();
    private readonly ChangeDetector _changeDetector = new();
    private readonly PromotionValidator _validator = new();

    public async Task<CrawlResult> RunAsync(CrawlRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = clock.UtcNow;
        string runId = startedAt.ToString(
            "yyyyMMddTHHmmssfffZ",
            System.Globalization.CultureInfo.InvariantCulture);
        List<SourceRunResult> sourceResults = [];
        List<Promotion> extracted = [];
        List<string> runWarnings = [];

        IReadOnlyList<SourceDefinition> selectedSources = request.Sources
            .Where(source => source.Enabled)
            .Where(source => request.SourceFilter is null ||
                             string.Equals(source.Id, request.SourceFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (SourceDefinition source in selectedSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset sourceStarted = clock.UtcNow;
            try
            {
                PageBatch batch = await pageSource.FetchAsync(
                    source,
                    request.Settings,
                    request.MaxPages,
                    request.DisablePlaywright,
                    cancellationToken);
                int sourcePromotionCount = 0;

                foreach (FetchedPage page in batch.Pages)
                {
                    IReadOnlyList<Promotion> pagePromotions = await extractor.ExtractAsync(
                        page,
                        source,
                        request.Municipalities,
                        cancellationToken);
                    foreach (Promotion promotion in pagePromotions)
                    {
                        if (request.MunicipalityFilter is not null &&
                            !string.Equals(
                                promotion.Municipality,
                                request.MunicipalityFilter,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        NormalizePromotion(promotion, source);
                        Promotion located = request.DisableGeocoding
                            ? promotion
                            : await geocoder.GeocodeAsync(
                                promotion,
                                request.Municipalities,
                                cancellationToken);
                        extracted.Add(located);
                        sourcePromotionCount++;
                    }
                }

                sourceResults.Add(new()
                {
                    SourceId = source.Id,
                    StartedAtUtc = sourceStarted,
                    FinishedAtUtc = clock.UtcNow,
                    Status = SourceRunStatus.Success,
                    UrlsDiscovered = batch.DiscoveredUrls,
                    UrlsFetched = batch.Pages.Count,
                    UrlsSkipped = batch.SkippedUrls,
                    PromotionsExtracted = sourcePromotionCount,
                    Warnings = batch.Warnings
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                sourceResults.Add(new()
                {
                    SourceId = source.Id,
                    StartedAtUtc = sourceStarted,
                    FinishedAtUtc = clock.UtcNow,
                    Status = SourceRunStatus.Failed,
                    Errors = [exception.Message]
                });
            }
        }

        IReadOnlyList<Promotion> deduplicated = _deduplicator.Deduplicate(extracted);
        IReadOnlyList<Promotion> previous = await stateRepository.LoadAsync(
            request.StateDirectory,
            cancellationToken);
        Dictionary<string, Promotion> previousById = previous.ToDictionary(
            promotion => promotion.Id,
            StringComparer.Ordinal);
        DateTimeOffset now = clock.UtcNow;
        List<PromotionChange> changes = [];
        List<Promotion> merged = [];

        foreach (Promotion promotion in deduplicated)
        {
            promotion.Id = PromotionIdentity.Create(promotion);
            previousById.TryGetValue(promotion.Id, out Promotion? old);
            promotion.FirstSeenUtc = old?.FirstSeenUtc ?? now;
            promotion.LastSeenUtc = now;
            promotion.ConsecutiveMisses = 0;
            promotion.Active = true;

            PromotionChange? change = _changeDetector.Detect(old, promotion, now);
            promotion.LastChangedUtc = change is null
                ? old?.LastChangedUtc ?? now
                : now;
            if (change is not null)
            {
                changes.Add(change);
            }

            merged.Add(promotion);
        }

        bool canCountMisses = sourceResults.Count > 0 &&
                              sourceResults.All(result => result.Status == SourceRunStatus.Success);
        HashSet<string> seenIds = merged.Select(promotion => promotion.Id).ToHashSet(StringComparer.Ordinal);
        foreach (Promotion old in previous.Where(promotion => !seenIds.Contains(promotion.Id)))
        {
            if (canCountMisses)
            {
                old.ConsecutiveMisses++;
                bool wasActive = old.Active;
                old.Active = old.ConsecutiveMisses < request.Settings.DeactivateAfterMisses;
                if (wasActive && !old.Active)
                {
                    old.LastChangedUtc = now;
                    PromotionChange? change = _changeDetector.Detect(
                        CloneWithActive(old, true),
                        old,
                        now);
                    if (change is not null)
                    {
                        changes.Add(change);
                    }
                }
            }

            merged.Add(old);
        }

        List<Promotion> valid = [];
        List<string> qualityIssues = [];
        int validationWarnings = 0;
        foreach (Promotion promotion in merged.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            ValidationResult validation = _validator.Validate(promotion, request.Municipalities);
            qualityIssues.AddRange(validation.Errors.Select(error => $"{promotion.Id}: {error}"));
            qualityIssues.AddRange(validation.Warnings.Select(warning => $"{promotion.Id}: {warning}"));
            validationWarnings += validation.Warnings.Count;
            if (validation.IsValid)
            {
                valid.Add(promotion);
            }
        }

        int failedSources = sourceResults.Count(result => result.Status == SourceRunStatus.Failed);
        int successfulSources = sourceResults.Count(result => result.Status == SourceRunStatus.Success);
        bool hasPublishableData = valid.Count > 0 && successfulSources > 0;
        RunStatus status = successfulSources == 0
            ? RunStatus.Failed
            : failedSources > 0 || qualityIssues.Count > validationWarnings
                ? RunStatus.PartialSuccess
                : RunStatus.Success;

        PromotionDataset dataset = CreateDataset(
            valid,
            runId,
            now,
            selectedSources.Count,
            successfulSources,
            failedSources);
        ChangeDataset changeDataset = new()
        {
            RunId = runId,
            GeneratedAtUtc = now,
            Changes = changes
                .OrderByDescending(change => change.DetectedAtUtc)
                .Take(request.Settings.PublicChangeLimit)
                .ToArray()
        };
        DateTimeOffset finishedAt = clock.UtcNow;
        RunReport run = CreateRunReport(
            runId,
            startedAt,
            finishedAt,
            status,
            sourceResults,
            dataset,
            changes,
            runWarnings,
            request.Settings,
            qualityIssues,
            validationWarnings,
            merged.Count - valid.Count);

        if (!request.DryRun && hasPublishableData)
        {
            await stateRepository.SaveAsync(request.StateDirectory, valid, cancellationToken);
            await publicDataWriter.WriteAsync(
                request.OutputDirectory,
                dataset,
                changeDataset,
                run,
                cancellationToken);
        }

        return new()
        {
            Dataset = dataset,
            Changes = changeDataset,
            Run = run,
            HasPublishableData = hasPublishableData
        };
    }

    private static void NormalizePromotion(Promotion promotion, SourceDefinition source)
    {
        promotion.Name = promotion.Name.Trim();
        promotion.NormalizedName = TextNormalizer.NormalizeForComparison(promotion.Name);
        promotion.CanonicalUrl = UrlNormalizer.Normalize(promotion.CanonicalUrl);
        promotion.SourceUrls = promotion.SourceUrls
            .Append(promotion.CanonicalUrl)
            .Select(UrlNormalizer.Normalize)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        promotion.SourceKind = source.SourceKind;
        promotion.SourceConfidence = SourceConfidenceScorer.Score(source, promotion);
        promotion.Evidence = promotion.Evidence
            .Select(item => new EvidenceItem
            {
                Field = item.Field,
                ValueText = TextNormalizer.CleanEvidence(item.ValueText, 160),
                SourceUrl = UrlNormalizer.Normalize(item.SourceUrl),
                CapturedAtUtc = item.CapturedAtUtc,
                Extractor = item.Extractor,
                Confidence = Math.Clamp(item.Confidence, 0m, 1m),
                Quality = item.Quality,
                TextFragment = TextNormalizer.CleanEvidence(item.TextFragment)
            })
            .ToArray();
    }

    private static PromotionDataset CreateDataset(
        IReadOnlyList<Promotion> promotions,
        string runId,
        DateTimeOffset now,
        int sourceCount,
        int successfulSources,
        int failedSources)
    {
        IReadOnlyList<Promotion> ordered = promotions
            .OrderBy(promotion => promotion.Municipality, StringComparer.Create(
                System.Globalization.CultureInfo.GetCultureInfo("es-ES"),
                ignoreCase: true))
            .ThenBy(promotion => promotion.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(promotion => promotion.Id, StringComparer.Ordinal)
            .ToArray();

        return new()
        {
            GeneratedAtUtc = now,
            RunId = runId,
            SourceCount = sourceCount,
            SuccessfulSourceCount = successfulSources,
            FailedSourceCount = failedSources,
            Promotions = ordered,
            Statistics = new()
            {
                Total = ordered.Count,
                Active = ordered.Count(promotion => promotion.Active),
                WithPrice = ordered.Count(promotion => promotion.PriceFrom.HasValue),
                WithCoordinates = ordered.Count(promotion =>
                    promotion.Latitude.HasValue && promotion.Longitude.HasValue),
                ByMunicipality = new SortedDictionary<string, int>(
                    ordered
                        .GroupBy(promotion => promotion.Municipality)
                        .ToDictionary(group => group.Key, group => group.Count()),
                    StringComparer.Ordinal)
            }
        };
    }

    private static RunReport CreateRunReport(
        string runId,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        RunStatus status,
        IReadOnlyList<SourceRunResult> sourceResults,
        PromotionDataset dataset,
        IReadOnlyList<PromotionChange> changes,
        IReadOnlyList<string> warnings,
        CrawlerSettings settings,
        IReadOnlyList<string> qualityIssues,
        int validationWarnings,
        int invalidPromotions)
    {
        DateTimeOffset? oldest = dataset.Promotions.Count == 0
            ? null
            : dataset.Promotions.Min(promotion => promotion.LastSeenUtc);
        DateTimeOffset? newest = dataset.Promotions.Count == 0
            ? null
            : dataset.Promotions.Max(promotion => promotion.LastSeenUtc);

        return new()
        {
            RunId = runId,
            StartedAtUtc = startedAt,
            FinishedAtUtc = finishedAt,
            DurationSeconds = Math.Round((finishedAt - startedAt).TotalSeconds, 3),
            Status = status,
            CrawlerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            TotalSources = sourceResults.Count,
            SuccessfulSources = sourceResults.Count(result => result.Status == SourceRunStatus.Success),
            FailedSources = sourceResults.Count(result => result.Status == SourceRunStatus.Failed),
            SkippedSources = sourceResults.Count(result => result.Status == SourceRunStatus.Skipped),
            DiscoveredUrls = sourceResults.Sum(result => result.UrlsDiscovered),
            FetchedUrls = sourceResults.Sum(result => result.UrlsFetched),
            SkippedUrls = sourceResults.Sum(result => result.UrlsSkipped),
            ExtractedPromotions = sourceResults.Sum(result => result.PromotionsExtracted),
            NewPromotions = changes.Count(change => change.Kind == ChangeKind.Added),
            ChangedPromotions = changes.Count(change => change.Kind != ChangeKind.Added),
            Warnings = warnings,
            SourceResults = sourceResults,
            DataFreshness = new()
            {
                OldestSourceCheckUtc = oldest,
                NewestSourceCheckUtc = newest,
                IsStale = newest.HasValue && finishedAt - newest.Value > TimeSpan.FromHours(settings.StaleAfterHours),
                StaleAfterHours = settings.StaleAfterHours
            },
            Quality = new()
            {
                ValidPromotions = dataset.Promotions.Count,
                InvalidPromotions = invalidPromotions,
                Warnings = validationWarnings,
                Issues = qualityIssues.Take(100).ToArray()
            }
        };
    }

    private static Promotion CloneWithActive(Promotion promotion, bool active)
    {
        return new()
        {
            Id = promotion.Id,
            Name = promotion.Name,
            NormalizedName = promotion.NormalizedName,
            Municipality = promotion.Municipality,
            CanonicalUrl = promotion.CanonicalUrl,
            Active = active,
            CommercialStatus = promotion.CommercialStatus,
            ConstructionStatus = promotion.ConstructionStatus,
            PriceFrom = promotion.PriceFrom,
            PriceTo = promotion.PriceTo,
            AvailableUnits = promotion.AvailableUnits,
            DeliveryDateText = promotion.DeliveryDateText,
            BuiltAreaMinSqm = promotion.BuiltAreaMinSqm,
            BuiltAreaMaxSqm = promotion.BuiltAreaMaxSqm,
            PlotAreaMinSqm = promotion.PlotAreaMinSqm,
            PlotAreaMaxSqm = promotion.PlotAreaMaxSqm
        };
    }
}
