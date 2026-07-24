using System.Security.Cryptography;
using System.Text;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Models;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Core.Discovery;

public sealed class OpportunityDiscoveryPipeline(
    IOpportunityFeedReader feedReader,
    IOpportunityStateRepository stateRepository,
    IClock clock)
{
    public async Task<OpportunityDiscoveryResult> RunAsync(
        OpportunityDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To)
        {
            throw new InvalidDataException("La fecha inicial del radar no puede superar la final.");
        }

        if (request.To.DayNumber - request.From.DayNumber > 366)
        {
            throw new InvalidDataException(
                "La ventana del radar no puede superar 367 días inclusivos.");
        }

        DateTimeOffset startedAt = clock.UtcNow;
        OpportunityRadarState previous = await stateRepository.LoadAsync(
            request.StateDirectory,
            cancellationToken);
        Dictionary<string, OpportunityCandidate> candidates = previous.Candidates.ToDictionary(
            candidate => candidate.Id,
            StringComparer.Ordinal);
        Dictionary<string, OpportunitySourceHealth> sourceHealth =
            previous.SourceHealth.ToDictionary(
                source => source.SourceId,
                StringComparer.Ordinal);
        List<OpportunitySourceRun> sourceRuns = [];
        int newCandidates = 0;
        int updatedCandidates = 0;
        HashSet<string> knownPromotionUrls = request.KnownPromotionUrls
            .Select(UrlNormalizer.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        OpportunitySourceDefinition[] enabledSources = request.Catalog.Sources
            .Where(source => source.Enabled)
            .OrderBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> enabledSourceIds = enabledSources
            .Select(source => source.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string removedSourceId in sourceHealth.Keys
                     .Where(sourceId => !enabledSourceIds.Contains(sourceId))
                     .ToArray())
        {
            sourceHealth.Remove(removedSourceId);
        }

        foreach (OpportunitySourceDefinition source in enabledSources)
        {
            sourceHealth[source.Id] = RegisterSource(
                source,
                sourceHealth.GetValueOrDefault(source.Id));
        }

        IEnumerable<OpportunitySourceDefinition> sources = enabledSources;
        if (!string.IsNullOrWhiteSpace(request.SourceFilter))
        {
            sources = sources.Where(source => string.Equals(
                source.Id,
                request.SourceFilter,
                StringComparison.OrdinalIgnoreCase));
        }

        foreach (OpportunitySourceDefinition source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<OpportunityFeedItem> items = await feedReader.ReadAsync(
                    source,
                    request.From,
                    request.To,
                    request.Municipalities,
                    cancellationToken);
                int matched = 0;
                foreach (OpportunityFeedItem item in items)
                {
                    OpportunityCandidate? candidate = Match(
                        source,
                        item,
                        request.Municipalities,
                        request.Catalog.Terms,
                        request.Catalog.ContextTerms,
                        request.Catalog.ExclusionTerms,
                        knownPromotionUrls,
                        clock.UtcNow);
                    if (candidate is null)
                    {
                        continue;
                    }

                    matched++;
                    if (candidates.TryGetValue(candidate.Id, out OpportunityCandidate? existing))
                    {
                        candidates[candidate.Id] = Merge(existing, candidate);
                        updatedCandidates++;
                    }
                    else
                    {
                        candidates[candidate.Id] = candidate;
                        newCandidates++;
                    }
                }

                sourceRuns.Add(new()
                {
                    SourceId = source.Id,
                    Success = true,
                    ItemsRead = items.Count,
                    CandidatesMatched = matched
                });
                sourceHealth[source.Id] = RecordSuccess(
                    source,
                    sourceHealth[source.Id],
                    items.Count,
                    matched,
                    clock.UtcNow);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or InvalidDataException)
            {
                string error = TextNormalizer.CleanEvidence(exception.Message);
                sourceRuns.Add(new()
                {
                    SourceId = source.Id,
                    Success = false,
                    Error = error
                });
                sourceHealth[source.Id] = RecordFailure(
                    source,
                    sourceHealth[source.Id],
                    error,
                    clock.UtcNow);
            }
        }

        updatedCandidates += ApplyConfiguredReviews(candidates, enabledSources);
        DateTimeOffset finishedAt = clock.UtcNow;
        OpportunityDiscoveryRun run = new()
        {
            RunId = $"op-{startedAt:yyyyMMddTHHmmssfffZ}",
            StartedAtUtc = startedAt,
            FinishedAtUtc = finishedAt,
            From = request.From,
            To = request.To,
            NewCandidates = newCandidates,
            UpdatedCandidates = updatedCandidates,
            Sources = sourceRuns.OrderBy(item => item.SourceId, StringComparer.Ordinal).ToArray()
        };
        OpportunitySourceHealth[] orderedHealth = sourceHealth.Values
            .OrderBy(item => item.SourceId, StringComparer.Ordinal)
            .ToArray();
        OpportunityCoverageSnapshot coverage = BuildCoverage(
            enabledSources,
            request.Municipalities,
            orderedHealth,
            candidates.Values,
            finishedAt);
        OpportunityRadarState state = new()
        {
            UpdatedAtUtc = finishedAt,
            LastRun = run,
            Candidates = candidates.Values
                .OrderBy(item => item.Municipality, StringComparer.Ordinal)
                .ThenByDescending(item => item.PublishedAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            SourceHealth = orderedHealth,
            Coverage = coverage
        };
        if (!request.DryRun)
        {
            await stateRepository.SaveAsync(request.StateDirectory, state, cancellationToken);
        }

        return new() { State = state, Run = run };
    }

    private static OpportunitySourceHealth RegisterSource(
        OpportunitySourceDefinition source,
        OpportunitySourceHealth? previous)
    {
        return new()
        {
            SourceId = source.Id,
            SourceName = source.Name,
            SourceKind = source.SourceKind,
            Cadence = source.Cadence,
            FixedMunicipality = source.FixedMunicipality,
            FirstCheckedUtc = previous?.FirstCheckedUtc,
            LastAttemptUtc = previous?.LastAttemptUtc,
            LastSuccessUtc = previous?.LastSuccessUtc,
            LastFailureUtc = previous?.LastFailureUtc,
            LastNonEmptyUtc = previous?.LastNonEmptyUtc,
            NextCheckDueUtc = previous?.NextCheckDueUtc,
            ConsecutiveFailures = previous?.ConsecutiveFailures ?? 0,
            ConsecutiveEmptyRuns = previous?.ConsecutiveEmptyRuns ?? 0,
            LastItemsRead = previous?.LastItemsRead ?? 0,
            LastCandidatesMatched = previous?.LastCandidatesMatched ?? 0,
            Status = previous?.Status ?? OpportunitySourceHealthStatus.NotChecked,
            Issues = previous?.Issues ?? []
        };
    }

    private static OpportunitySourceHealth RecordSuccess(
        OpportunitySourceDefinition source,
        OpportunitySourceHealth previous,
        int itemsRead,
        int candidatesMatched,
        DateTimeOffset checkedAtUtc)
    {
        int consecutiveEmptyRuns = itemsRead == 0
            ? previous.ConsecutiveEmptyRuns + 1
            : 0;
        bool emptyAnomaly = itemsRead == 0 &&
                            consecutiveEmptyRuns >= 2 &&
                            previous.LastNonEmptyUtc.HasValue;
        return new()
        {
            SourceId = source.Id,
            SourceName = source.Name,
            SourceKind = source.SourceKind,
            Cadence = source.Cadence,
            FixedMunicipality = source.FixedMunicipality,
            FirstCheckedUtc = previous.FirstCheckedUtc ?? checkedAtUtc,
            LastAttemptUtc = checkedAtUtc,
            LastSuccessUtc = checkedAtUtc,
            LastFailureUtc = previous.LastFailureUtc,
            LastNonEmptyUtc = itemsRead > 0 ? checkedAtUtc : previous.LastNonEmptyUtc,
            NextCheckDueUtc = NextCheckDue(checkedAtUtc),
            ConsecutiveFailures = 0,
            ConsecutiveEmptyRuns = consecutiveEmptyRuns,
            LastItemsRead = itemsRead,
            LastCandidatesMatched = candidatesMatched,
            Status = emptyAnomaly
                ? OpportunitySourceHealthStatus.Degraded
                : OpportunitySourceHealthStatus.Healthy,
            Issues = emptyAnomaly
                ? ["La fuente devolvió cero entradas en dos ejecuciones consecutivas tras contener datos."]
                : []
        };
    }

    private static OpportunitySourceHealth RecordFailure(
        OpportunitySourceDefinition source,
        OpportunitySourceHealth previous,
        string error,
        DateTimeOffset checkedAtUtc)
    {
        int consecutiveFailures = previous.ConsecutiveFailures + 1;
        return new()
        {
            SourceId = source.Id,
            SourceName = source.Name,
            SourceKind = source.SourceKind,
            Cadence = source.Cadence,
            FixedMunicipality = source.FixedMunicipality,
            FirstCheckedUtc = previous.FirstCheckedUtc ?? checkedAtUtc,
            LastAttemptUtc = checkedAtUtc,
            LastSuccessUtc = previous.LastSuccessUtc,
            LastFailureUtc = checkedAtUtc,
            LastNonEmptyUtc = previous.LastNonEmptyUtc,
            NextCheckDueUtc = NextCheckDue(checkedAtUtc),
            ConsecutiveFailures = consecutiveFailures,
            ConsecutiveEmptyRuns = previous.ConsecutiveEmptyRuns,
            LastItemsRead = previous.LastItemsRead,
            LastCandidatesMatched = previous.LastCandidatesMatched,
            Status = consecutiveFailures >= 2
                ? OpportunitySourceHealthStatus.Failing
                : OpportunitySourceHealthStatus.Degraded,
            Issues = [error]
        };
    }

    private static DateTimeOffset NextCheckDue(DateTimeOffset checkedAtUtc)
    {
        return checkedAtUtc.AddHours(36);
    }

    private static OpportunityCoverageSnapshot BuildCoverage(
        IReadOnlyList<OpportunitySourceDefinition> sources,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        IReadOnlyList<OpportunitySourceHealth> sourceHealth,
        IEnumerable<OpportunityCandidate> candidates,
        DateTimeOffset generatedAtUtc)
    {
        Dictionary<string, OpportunitySourceHealth> healthBySource = sourceHealth.ToDictionary(
            source => source.SourceId,
            StringComparer.Ordinal);
        OpportunitySourceDefinition[] centralSources = sources
            .Where(source => source.SourceKind is
                OpportunitySourceKind.RegionalGazette or
                OpportunitySourceKind.StateGazette or
                OpportunitySourceKind.PublicProcurement or
                OpportunitySourceKind.PublicLandPortal)
            .ToArray();
        OpportunityCandidate[] candidateArray = candidates.ToArray();
        OpportunitySourceDefinition[] commercialSources = sources
            .Where(source =>
                source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite)
            .ToArray();
        HashSet<string> commercialDomains = CommercialDomains(commercialSources);
        HashSet<string> healthyCommercialSourceIds = sourceHealth
            .Where(source =>
                source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite &&
                source.Status == OpportunitySourceHealthStatus.Healthy)
            .Select(source => source.SourceId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> healthyCommercialDomains = CommercialDomains(
            commercialSources.Where(source => healthyCommercialSourceIds.Contains(source.Id)));
        HashSet<string> referencedDomains = candidateArray
            .Where(candidate => candidate.Status is
                OpportunityCandidateStatus.New or
                OpportunityCandidateStatus.Monitoring or
                OpportunityCandidateStatus.VerifiedSource)
            .SelectMany(candidate => candidate.RelatedUrls)
            .Select(TryGetDomain)
            .Where(domain => domain is not null)
            .Select(domain => domain!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        MunicipalityOpportunityCoverage[] municipalityCoverage = municipalities
            .Where(municipality => municipality.Enabled)
            .OrderBy(municipality => municipality.OfficialName, StringComparer.Ordinal)
            .Select(municipality =>
            {
                OpportunitySourceDefinition[] directSources = sources
                    .Where(source =>
                        source.SourceKind == OpportunitySourceKind.MunicipalNoticeBoard &&
                        string.Equals(
                            source.FixedMunicipality,
                            municipality.OfficialName,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                OpportunitySourceHealth[] relevantHealth = directSources
                    .Concat(centralSources)
                    .Select(source => healthBySource[source.Id])
                    .ToArray();
                OpportunitySourceHealth[] healthyDirect = directSources
                    .Select(source => healthBySource[source.Id])
                    .Where(health => health.Status == OpportunitySourceHealthStatus.Healthy)
                    .ToArray();
                OpportunitySourceHealth[] healthyCentral = centralSources
                    .Select(source => healthBySource[source.Id])
                    .Where(health => health.Status == OpportunitySourceHealthStatus.Healthy)
                    .ToArray();
                bool anyChecked = relevantHealth.Any(health => health.LastAttemptUtc.HasValue);
                MunicipalityCoverageStatus status = ResolveCoverageStatus(
                    healthyDirect.Length,
                    healthyCentral.Length,
                    anyChecked);
                OpportunityCandidate[] municipalityCandidates = candidateArray
                    .Where(candidate => string.Equals(
                        candidate.Municipality,
                        municipality.OfficialName,
                        StringComparison.Ordinal))
                    .ToArray();
                return new MunicipalityOpportunityCoverage
                {
                    Municipality = municipality.OfficialName,
                    Status = status,
                    ConfiguredDirectSources = directSources.Length,
                    HealthyDirectSources = healthyDirect.Length,
                    HealthyCentralSources = healthyCentral.Length,
                    LastSuccessfulCheckUtc = healthyDirect
                        .Concat(healthyCentral)
                        .Select(health => health.LastSuccessUtc)
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value)
                        .DefaultIfEmpty()
                        .Max() is DateTimeOffset lastSuccess && lastSuccess != default
                            ? lastSuccess
                            : null,
                    NewCandidates = municipalityCandidates.Count(candidate =>
                        candidate.Status == OpportunityCandidateStatus.New),
                    MonitoringCandidates = municipalityCandidates.Count(candidate =>
                        candidate.Status == OpportunityCandidateStatus.Monitoring)
                };
            })
            .ToArray();

        return new()
        {
            GeneratedAtUtc = generatedAtUtc,
            EnabledSources = sources.Count,
            HealthySources = sourceHealth.Count(source =>
                source.Status == OpportunitySourceHealthStatus.Healthy),
            DegradedSources = sourceHealth.Count(source =>
                source.Status == OpportunitySourceHealthStatus.Degraded),
            FailingSources = sourceHealth.Count(source =>
                source.Status == OpportunitySourceHealthStatus.Failing),
            CommercialSources = commercialSources.Length,
            HealthyCommercialSources = healthyCommercialSourceIds.Count,
            CommercialDomainsMonitored = commercialDomains.Count,
            HealthyCommercialDomains = healthyCommercialDomains.Count,
            ReferencedDomainsDiscovered = referencedDomains.Count,
            UnmonitoredReferencedDomains = referencedDomains.Count(domain =>
                !commercialDomains.Contains(domain)),
            MunicipalitiesTotal = municipalityCoverage.Length,
            MunicipalitiesWithDirectSource = municipalityCoverage.Count(item =>
                item.ConfiguredDirectSources > 0),
            MunicipalitiesWithHealthyDirectSource = municipalityCoverage.Count(item =>
                item.HealthyDirectSources > 0),
            MunicipalitiesWithHealthyCoverage = municipalityCoverage.Count(item =>
                item.Status is
                    MunicipalityCoverageStatus.CentralOnly or
                    MunicipalityCoverageStatus.DirectOnly or
                    MunicipalityCoverageStatus.DirectAndCentral),
            PendingCandidates = candidateArray.Count(candidate =>
                candidate.Status is
                    OpportunityCandidateStatus.New or
                    OpportunityCandidateStatus.Monitoring),
            NewCandidates = candidateArray.Count(candidate =>
                candidate.Status == OpportunityCandidateStatus.New),
            MonitoringCandidates = candidateArray.Count(candidate =>
                candidate.Status == OpportunityCandidateStatus.Monitoring),
            RejectedCandidates = candidateArray.Count(candidate =>
                candidate.Status == OpportunityCandidateStatus.Rejected),
            VerifiedSourceCandidates = candidateArray.Count(candidate =>
                candidate.Status == OpportunityCandidateStatus.VerifiedSource),
            StaleCandidates = candidateArray.Count(candidate =>
                candidate.Status == OpportunityCandidateStatus.Stale),
            MunicipalitiesWithCommercialSignals = candidateArray
                .Where(candidate =>
                    candidate.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite)
                .Select(candidate => candidate.Municipality)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            Municipalities = municipalityCoverage
        };
    }

    private static HashSet<string> CommercialDomains(
        IEnumerable<OpportunitySourceDefinition> sources)
    {
        return sources
            .SelectMany(source => source.AllowedHosts)
            .Select(host => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? host[4..]
                : host)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? TryGetDomain(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri.IdnHost.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.IdnHost[4..]
            : uri.IdnHost;
    }

    private static MunicipalityCoverageStatus ResolveCoverageStatus(
        int healthyDirectSources,
        int healthyCentralSources,
        bool anyChecked)
    {
        if (healthyDirectSources > 0 && healthyCentralSources > 0)
        {
            return MunicipalityCoverageStatus.DirectAndCentral;
        }

        if (healthyDirectSources > 0)
        {
            return MunicipalityCoverageStatus.DirectOnly;
        }

        if (healthyCentralSources > 0)
        {
            return MunicipalityCoverageStatus.CentralOnly;
        }

        return anyChecked
            ? MunicipalityCoverageStatus.Degraded
            : MunicipalityCoverageStatus.NotChecked;
    }

    private static OpportunityCandidate? Match(
        OpportunitySourceDefinition source,
        OpportunityFeedItem item,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        IReadOnlyList<OpportunityTermRule> rules,
        IReadOnlyList<string> contextTerms,
        IReadOnlyList<string> exclusionTerms,
        IReadOnlySet<string> knownPromotionUrls,
        DateTimeOffset observedAtUtc)
    {
        string searchable = TextNormalizer.NormalizeForComparison(
            $"{item.Title} {item.Summary}");
        MunicipalityDefinition? municipality = !string.IsNullOrWhiteSpace(
            item.MunicipalityHint)
            ? municipalities.FirstOrDefault(candidate =>
                candidate.Enabled &&
                string.Equals(
                    candidate.OfficialName,
                    item.MunicipalityHint,
                    StringComparison.OrdinalIgnoreCase))
            : string.IsNullOrWhiteSpace(source.FixedMunicipality)
                ? FindMunicipality(searchable, municipalities)
                : municipalities.FirstOrDefault(candidate =>
                candidate.Enabled &&
                string.Equals(
                    candidate.OfficialName,
                    source.FixedMunicipality,
                    StringComparison.OrdinalIgnoreCase));
        OpportunityTermRule[] matchedRules = rules
            .Where(rule => ContainsTerm(searchable, rule.Term))
            .ToArray();
        bool hasContext = contextTerms.Any(term => ContainsTerm(searchable, term));
        bool isExcluded = !source.IgnoreExclusionTerms &&
                          exclusionTerms.Any(term => ContainsTerm(searchable, term));
        if (municipality is null || matchedRules.Length == 0 || !hasContext || isExcluded)
        {
            return null;
        }

        OpportunityKind kind = matchedRules
            .GroupBy(rule => rule.Kind)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .First();
        string identity = string.Join(
            '|',
            source.Id,
            string.IsNullOrWhiteSpace(item.ExternalId) ? item.OfficialUrl : item.ExternalId,
            municipality.OfficialName);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        decimal confidenceBase =
            source.SourceKind == OpportunitySourceKind.WebSearch ? 0.45m : 0.65m;
        decimal confidenceMaximum =
            source.SourceKind == OpportunitySourceKind.WebSearch ? 0.7m : 0.9m;
        decimal confidence = Math.Min(
            confidenceMaximum,
            confidenceBase + ((matchedRules.Length - 1) * 0.05m));

        return new()
        {
            Id = $"lead-{Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant()}",
            SourceId = source.Id,
            SourceName = source.Name,
            SourceKind = source.SourceKind,
            ExternalId = TextNormalizer.CleanEvidence(item.ExternalId, 160),
            Title = TextNormalizer.CleanEvidence(item.Title, 240),
            Summary = TextNormalizer.CleanEvidence(item.Summary, 800),
            OfficialUrl = item.OfficialUrl,
            RelatedUrls = item.RelatedUrls
                .Where(value =>
                    Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
                    uri.Scheme == Uri.UriSchemeHttps)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray(),
            PublishedAtUtc = item.PublishedAtUtc,
            Municipality = municipality.OfficialName,
            Kind = kind,
            Confidence = confidence,
            MatchedTerms = matchedRules.Select(rule => rule.Term)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            FirstSeenUtc = observedAtUtc,
            LastSeenUtc = observedAtUtc,
            Status = ResolveCandidateStatus(source, item, knownPromotionUrls)
        };
    }

    private static OpportunityCandidateStatus ResolveCandidateStatus(
        OpportunitySourceDefinition source,
        OpportunityFeedItem item,
        IReadOnlySet<string> knownPromotionUrls)
    {
        if (knownPromotionUrls.Contains(UrlNormalizer.Normalize(item.OfficialUrl)))
        {
            return OpportunityCandidateStatus.VerifiedSource;
        }

        return source.ReviewRules
            .FirstOrDefault(rule => item.OfficialUrl.Contains(
                rule.UrlPattern,
                StringComparison.OrdinalIgnoreCase))
            ?.Status ?? OpportunityCandidateStatus.New;
    }

    private static int ApplyConfiguredReviews(
        IDictionary<string, OpportunityCandidate> candidates,
        IEnumerable<OpportunitySourceDefinition> sources)
    {
        Dictionary<string, OpportunitySourceDefinition> sourcesById = sources
            .ToDictionary(source => source.Id, StringComparer.Ordinal);
        int updated = 0;
        foreach ((string id, OpportunityCandidate candidate) in candidates.ToArray())
        {
            if (candidate.Status is not (
                    OpportunityCandidateStatus.New or
                    OpportunityCandidateStatus.Monitoring) ||
                !sourcesById.TryGetValue(
                    candidate.SourceId,
                    out OpportunitySourceDefinition? source))
            {
                continue;
            }

            OpportunityCandidateStatus? reviewedStatus = source.ReviewRules
                .FirstOrDefault(rule => candidate.OfficialUrl.Contains(
                    rule.UrlPattern,
                    StringComparison.OrdinalIgnoreCase))
                ?.Status;
            if (!reviewedStatus.HasValue ||
                reviewedStatus.Value == candidate.Status)
            {
                continue;
            }

            candidates[id] = CopyWithStatus(candidate, reviewedStatus.Value);
            updated++;
        }

        return updated;
    }

    private static OpportunityCandidate CopyWithStatus(
        OpportunityCandidate candidate,
        OpportunityCandidateStatus status)
    {
        return new()
        {
            Id = candidate.Id,
            SourceId = candidate.SourceId,
            SourceName = candidate.SourceName,
            SourceKind = candidate.SourceKind,
            ExternalId = candidate.ExternalId,
            Title = candidate.Title,
            Summary = candidate.Summary,
            OfficialUrl = candidate.OfficialUrl,
            RelatedUrls = candidate.RelatedUrls,
            PublishedAtUtc = candidate.PublishedAtUtc,
            Municipality = candidate.Municipality,
            Kind = candidate.Kind,
            Confidence = candidate.Confidence,
            MatchedTerms = candidate.MatchedTerms,
            FirstSeenUtc = candidate.FirstSeenUtc,
            LastSeenUtc = candidate.LastSeenUtc,
            Status = status
        };
    }

    private static MunicipalityDefinition? FindMunicipality(
        string searchable,
        IReadOnlyList<MunicipalityDefinition> municipalities)
    {
        return municipalities
            .Where(municipality => municipality.Enabled)
            .Select(municipality => new
            {
                Municipality = municipality,
                MatchLength = MunicipalityTerms(municipality)
                    .Where(term => ContainsNormalizedTerm(searchable, term))
                    .Select(term => term.Length)
                    .DefaultIfEmpty()
                    .Max()
            })
            .Where(match => match.MatchLength > 0)
            .OrderByDescending(match => match.MatchLength)
            .Select(match => match.Municipality)
            .FirstOrDefault();
    }

    private static IEnumerable<string> MunicipalityTerms(MunicipalityDefinition municipality)
    {
        yield return TextNormalizer.NormalizeForComparison(municipality.OfficialName);
        foreach (string locality in municipality.Localities)
        {
            yield return TextNormalizer.NormalizeForComparison(locality);
        }

        foreach (string alias in municipality.Aliases)
        {
            string normalized = TextNormalizer.NormalizeForComparison(alias);
            if (normalized.Length >= 7)
            {
                yield return normalized;
            }
        }
    }

    private static bool ContainsTerm(string searchable, string term)
    {
        return ContainsNormalizedTerm(
            searchable,
            TextNormalizer.NormalizeForComparison(term));
    }

    private static bool ContainsNormalizedTerm(string searchable, string normalizedTerm)
    {
        return normalizedTerm.Length > 0 &&
               $" {searchable} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);
    }

    private static OpportunityCandidate Merge(
        OpportunityCandidate existing,
        OpportunityCandidate current)
    {
        return new()
        {
            Id = existing.Id,
            SourceId = current.SourceId,
            SourceName = current.SourceName,
            SourceKind = current.SourceKind,
            ExternalId = current.ExternalId,
            Title = current.Title,
            Summary = current.Summary,
            OfficialUrl = current.OfficialUrl,
            RelatedUrls = current.RelatedUrls,
            PublishedAtUtc = current.PublishedAtUtc,
            Municipality = current.Municipality,
            Kind = current.Kind,
            Confidence = current.Confidence,
            MatchedTerms = current.MatchedTerms,
            FirstSeenUtc = existing.FirstSeenUtc,
            LastSeenUtc = current.LastSeenUtc,
            Status = MergeStatus(existing.Status, current.Status)
        };
    }

    private static OpportunityCandidateStatus MergeStatus(
        OpportunityCandidateStatus existing,
        OpportunityCandidateStatus current)
    {
        if (current == OpportunityCandidateStatus.VerifiedSource)
        {
            return current;
        }

        bool canApplyReview = existing is
            OpportunityCandidateStatus.New or OpportunityCandidateStatus.Monitoring;
        return canApplyReview && current != OpportunityCandidateStatus.New
                ? current
                : existing;
    }
}
