using System.Globalization;
using System.Text;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Discovery;

public sealed class OpportunityTriageService
{
    private static readonly string[] DirectPromotionTerms =
    [
        "obra nueva",
        "nueva promocion",
        "promocion",
        "residencial",
        "chalets",
        "viviendas unifamiliares",
        "cooperativa"
    ];

    private static readonly string[] AdministrativeTerms =
    [
        "licencia de obra",
        "plan parcial",
        "estudio de detalle",
        "suelo residencial",
        "parcela",
        "derecho de superficie",
        "urbanizacion"
    ];

    private static readonly string[] GenericAdministrativeTerms =
    [
        "borme",
        "modelo de solicitud",
        "normas subsidiarias",
        "ordenanza fiscal",
        "publicaciones en el bocm",
        "solicitud de licencia",
        "texto refundido"
    ];

    public OpportunityTriageReport Create(
        OpportunityRadarState state,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<string>? excludedListingHosts = null)
    {
        HashSet<string> excludedHosts = (excludedListingHosts ?? [])
            .Select(host => host.Trim().ToLowerInvariant())
            .Where(host => host.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        OpportunityCandidate[] pending = state.Candidates
            .Where(candidate => candidate.Status is
                OpportunityCandidateStatus.New or
                OpportunityCandidateStatus.Monitoring)
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, string> duplicateMasters = FindDuplicateMasters(pending);
        OpportunityTriageItem[] items = pending
            .Select(candidate => BuildItem(
                candidate,
                generatedAtUtc,
                excludedHosts,
                duplicateMasters.GetValueOrDefault(candidate.Id)))
            .OrderBy(item => PriorityOrder(item.Priority))
            .ThenByDescending(item => item.PriorityScore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.Municipality, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToArray();
        OpportunityTriageDomain[] domains = items
            .GroupBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OpportunityTriageDomain
            {
                Domain = group.Key,
                Candidates = group.Count(),
                HighPriority = group.Count(item =>
                    item.Priority == OpportunityTriagePriority.High),
                MediumPriority = group.Count(item =>
                    item.Priority == OpportunityTriagePriority.Medium),
                LowPriority = group.Count(item =>
                    item.Priority == OpportunityTriagePriority.Low),
                PossibleDuplicates = group.Count(item =>
                    item.Priority == OpportunityTriagePriority.Duplicate)
            })
            .OrderByDescending(domain => domain.HighPriority)
            .ThenByDescending(domain => domain.Candidates)
            .ThenBy(domain => domain.Domain, StringComparer.Ordinal)
            .ToArray();

        return new()
        {
            GeneratedAtUtc = generatedAtUtc,
            StateUpdatedAtUtc = state.UpdatedAtUtc,
            PendingCandidates = items.Length,
            HighPriority = items.Count(item =>
                item.Priority == OpportunityTriagePriority.High),
            MediumPriority = items.Count(item =>
                item.Priority == OpportunityTriagePriority.Medium),
            LowPriority = items.Count(item =>
                item.Priority == OpportunityTriagePriority.Low),
            PossibleDuplicates = items.Count(item =>
                item.Priority == OpportunityTriagePriority.Duplicate),
            Domains = domains,
            Items = items
        };
    }

    private static OpportunityTriageItem BuildItem(
        OpportunityCandidate candidate,
        DateTimeOffset generatedAtUtc,
        IReadOnlySet<string> excludedListingHosts,
        string? duplicateOfCandidateId)
    {
        string corpus = Normalize(
            $"{candidate.Title} {candidate.Summary} {candidate.OfficialUrl}");
        List<OpportunityTriageReason> reasons = [];
        bool hasDirectTerms = ContainsAny(corpus, DirectPromotionTerms);
        bool hasAdministrativeTerms = ContainsAny(corpus, AdministrativeTerms);
        string domain = GetDomain(candidate.OfficialUrl);
        int score = decimal.ToInt32(decimal.Round(
            candidate.Confidence * 100m,
            0,
            MidpointRounding.AwayFromZero));

        if (hasDirectTerms)
        {
            score += 15;
            reasons.Add(OpportunityTriageReason.DirectPromotionTerms);
        }

        if (hasAdministrativeTerms)
        {
            score += 12;
            reasons.Add(OpportunityTriageReason.AdministrativeTerms);
        }

        if (IsPublicAdministrationHost(domain))
        {
            score += 12;
            reasons.Add(OpportunityTriageReason.PublicAdministrationHost);
        }

        if (candidate.MatchedTerms.Distinct(StringComparer.OrdinalIgnoreCase).Count() >= 2)
        {
            score += 5;
            reasons.Add(OpportunityTriageReason.MultipleMatchedTerms);
        }

        if (candidate.PublishedAtUtc.HasValue &&
            candidate.PublishedAtUtc.Value <= generatedAtUtc &&
            candidate.PublishedAtUtc.Value >= generatedAtUtc.AddDays(-60))
        {
            score += 4;
            reasons.Add(OpportunityTriageReason.RecentlyPublished);
        }

        string titleAndUrl = Normalize($"{candidate.Title} {candidate.OfficialUrl}");
        string summary = Normalize(candidate.Summary);
        string municipality = Normalize(candidate.Municipality);
        if (ContainsLocation(titleAndUrl, municipality))
        {
            score += 10;
            reasons.Add(OpportunityTriageReason.MunicipalityInTitleOrUrl);
        }
        else if (ContainsLocation(summary, municipality))
        {
            score -= 8;
            reasons.Add(OpportunityTriageReason.MunicipalityOnlyInSummary);
        }
        else
        {
            score -= 20;
            reasons.Add(OpportunityTriageReason.MunicipalityUnconfirmed);
        }

        if (!candidate.PublishedAtUtc.HasValue &&
            ContainsHistoricalYear(
                $"{candidate.Title} {candidate.OfficialUrl}",
                generatedAtUtc.Year))
        {
            score -= 12;
            reasons.Add(OpportunityTriageReason.HistoricalReference);
        }

        if (ContainsAny(Normalize(candidate.Title), GenericAdministrativeTerms))
        {
            score -= 20;
            reasons.Add(OpportunityTriageReason.GenericAdministrativeReference);
        }

        if (HostMatches(domain, excludedListingHosts))
        {
            score -= 40;
            reasons.Add(OpportunityTriageReason.ExcludedListingHost);
        }

        OpportunityTriageBand band = hasDirectTerms
            ? OpportunityTriageBand.DirectPromotion
            : hasAdministrativeTerms
                ? OpportunityTriageBand.AdministrativeSignal
                : OpportunityTriageBand.GeneralSignal;
        OpportunityTriagePriority priority = score >= 75
            ? OpportunityTriagePriority.High
            : score >= 60
                ? OpportunityTriagePriority.Medium
                : OpportunityTriagePriority.Low;
        if (!string.IsNullOrWhiteSpace(duplicateOfCandidateId))
        {
            band = OpportunityTriageBand.PossibleDuplicate;
            priority = OpportunityTriagePriority.Duplicate;
            reasons.Add(OpportunityTriageReason.PossibleDuplicate);
        }

        return new()
        {
            CandidateId = candidate.Id,
            SourceId = candidate.SourceId,
            SourceKind = candidate.SourceKind,
            Title = candidate.Title,
            Summary = candidate.Summary,
            OfficialUrl = candidate.OfficialUrl,
            Domain = domain,
            Municipality = candidate.Municipality,
            Kind = candidate.Kind,
            Confidence = candidate.Confidence,
            PublishedAtUtc = candidate.PublishedAtUtc,
            FirstSeenUtc = candidate.FirstSeenUtc,
            LastSeenUtc = candidate.LastSeenUtc,
            Status = candidate.Status,
            Band = band,
            Priority = priority,
            PriorityScore = Math.Clamp(score, 0, 100),
            Reasons = reasons,
            DuplicateOfCandidateId = duplicateOfCandidateId
        };
    }

    private static Dictionary<string, string> FindDuplicateMasters(
        IReadOnlyList<OpportunityCandidate> candidates)
    {
        Dictionary<string, string> duplicates = new(StringComparer.Ordinal);
        foreach (IGrouping<string, OpportunityCandidate> group in candidates
                     .Where(candidate => Normalize(candidate.Title).Length >= 8)
                     .GroupBy(
                         candidate =>
                             $"{Normalize(candidate.Municipality)}|" +
                             $"{Normalize(candidate.Title)}",
                         StringComparer.Ordinal))
        {
            OpportunityCandidate[] ordered = group
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.FirstSeenUtc)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length < 2)
            {
                continue;
            }

            foreach (OpportunityCandidate duplicate in ordered.Skip(1))
            {
                duplicates[duplicate.Id] = ordered[0].Id;
            }
        }

        return duplicates;
    }

    private static bool ContainsAny(string corpus, IEnumerable<string> terms)
    {
        return terms.Any(term => corpus.Contains(
            Normalize(term),
            StringComparison.Ordinal));
    }

    private static bool ContainsLocation(string corpus, string municipality)
    {
        if (municipality.Length == 0)
        {
            return false;
        }

        return corpus.Contains(municipality, StringComparison.Ordinal) ||
               Compact(corpus).Contains(Compact(municipality), StringComparison.Ordinal);
    }

    private static string Compact(string value)
    {
        return new(
            value.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool ContainsHistoricalYear(string value, int currentYear)
    {
        int latestHistoricalYear = currentYear - 2;
        for (int year = 2000; year <= latestHistoricalYear; year++)
        {
            if (value.Contains(
                    year.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HostMatches(
        string domain,
        IReadOnlySet<string> excludedListingHosts)
    {
        return excludedListingHosts.Any(excluded =>
            string.Equals(domain, excluded, StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith($".{excluded}", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDomain(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? uri.IdnHost.ToLowerInvariant()
            : string.Empty;
    }

    private static bool IsPublicAdministrationHost(string domain)
    {
        return domain.EndsWith(".gob.es", StringComparison.Ordinal) ||
               domain.EndsWith(".madrid.org", StringComparison.Ordinal) ||
               string.Equals(domain, "boe.es", StringComparison.Ordinal) ||
               domain.EndsWith(".boe.es", StringComparison.Ordinal) ||
               string.Equals(domain, "bocm.es", StringComparison.Ordinal) ||
               domain.EndsWith(".bocm.es", StringComparison.Ordinal) ||
               string.Equals(
                   domain,
                   "contrataciondelestado.es",
                   StringComparison.Ordinal) ||
               domain.EndsWith(
                   ".contrataciondelestado.es",
                   StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        StringBuilder builder = new();
        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return string.Join(
            ' ',
            builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));
    }

    private static int PriorityOrder(OpportunityTriagePriority priority)
    {
        return priority switch
        {
            OpportunityTriagePriority.High => 0,
            OpportunityTriagePriority.Medium => 1,
            OpportunityTriagePriority.Low => 2,
            OpportunityTriagePriority.Duplicate => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(priority))
        };
    }
}
