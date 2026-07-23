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
        List<OpportunitySourceRun> sourceRuns = [];
        int newCandidates = 0;
        int updatedCandidates = 0;

        IEnumerable<OpportunitySourceDefinition> sources = request.Catalog.Sources
            .Where(source => source.Enabled);
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
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException or InvalidDataException)
            {
                sourceRuns.Add(new()
                {
                    SourceId = source.Id,
                    Success = false,
                    Error = TextNormalizer.CleanEvidence(exception.Message)
                });
            }
        }

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
        OpportunityRadarState state = new()
        {
            UpdatedAtUtc = finishedAt,
            LastRun = run,
            Candidates = candidates.Values
                .OrderBy(item => item.Municipality, StringComparer.Ordinal)
                .ThenByDescending(item => item.PublishedAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray()
        };
        if (!request.DryRun)
        {
            await stateRepository.SaveAsync(request.StateDirectory, state, cancellationToken);
        }

        return new() { State = state, Run = run };
    }

    private static OpportunityCandidate? Match(
        OpportunitySourceDefinition source,
        OpportunityFeedItem item,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        IReadOnlyList<OpportunityTermRule> rules,
        IReadOnlyList<string> contextTerms,
        IReadOnlyList<string> exclusionTerms,
        DateTimeOffset observedAtUtc)
    {
        string searchable = TextNormalizer.NormalizeForComparison(
            $"{item.Title} {item.Summary}");
        MunicipalityDefinition? municipality = string.IsNullOrWhiteSpace(
            source.FixedMunicipality)
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
        bool isExcluded = exclusionTerms.Any(term => ContainsTerm(searchable, term));
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
        decimal confidence = Math.Min(0.9m, 0.65m + ((matchedRules.Length - 1) * 0.05m));

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
            PublishedAtUtc = item.PublishedAtUtc,
            Municipality = municipality.OfficialName,
            Kind = kind,
            Confidence = confidence,
            MatchedTerms = matchedRules.Select(rule => rule.Term)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            FirstSeenUtc = observedAtUtc,
            LastSeenUtc = observedAtUtc
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
            PublishedAtUtc = current.PublishedAtUtc,
            Municipality = current.Municipality,
            Kind = current.Kind,
            Confidence = current.Confidence,
            MatchedTerms = current.MatchedTerms,
            FirstSeenUtc = existing.FirstSeenUtc,
            LastSeenUtc = current.LastSeenUtc,
            Status = existing.Status
        };
    }
}
