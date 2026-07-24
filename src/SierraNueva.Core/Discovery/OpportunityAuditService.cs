using SierraNueva.Contracts;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Discovery;

public sealed class OpportunityAuditService
{
    public OpportunityAuditReport Create(
        OpportunityRadarState state,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        int sampleSize,
        DateOnly from,
        DateOnly to,
        DateTimeOffset generatedAtUtc)
    {
        if (sampleSize < 1)
        {
            throw new InvalidDataException("La muestra debe contener al menos un municipio.");
        }

        if (from > to)
        {
            throw new InvalidDataException(
                "La fecha inicial de la auditoría no puede superar la final.");
        }

        Dictionary<string, MunicipalityOpportunityCoverage> coverageByMunicipality =
            state.Coverage.Municipalities.ToDictionary(
                item => item.Municipality,
                StringComparer.OrdinalIgnoreCase);
        OpportunityCandidate[] observedCandidates = state.Candidates
            .Where(candidate => candidate.Status is not (
                OpportunityCandidateStatus.Rejected or
                OpportunityCandidateStatus.Stale) &&
                ObservedWithin(candidate, from, to))
            .ToArray();
        OpportunityAuditMunicipality[] population = municipalities
            .Where(municipality => municipality.Enabled)
            .OrderBy(municipality => municipality.OfficialName, StringComparer.Ordinal)
            .Select(municipality => BuildMunicipality(
                municipality.OfficialName,
                coverageByMunicipality.GetValueOrDefault(municipality.OfficialName),
                observedCandidates))
            .ToArray();
        OpportunityAuditMunicipality[] sample = population
            .OrderBy(item => Priority(item.Reason))
            .ThenByDescending(item => item.PendingCandidates)
            .ThenByDescending(item =>
                item.CentralCandidates +
                item.DirectCandidates +
                item.CommercialCandidates)
            .ThenBy(item => item.Municipality, StringComparer.Ordinal)
            .Take(Math.Min(sampleSize, population.Length))
            .ToArray();

        return new()
        {
            GeneratedAtUtc = generatedAtUtc,
            StateUpdatedAtUtc = state.UpdatedAtUtc,
            From = from,
            To = to,
            Population = population.Length,
            RequestedSampleSize = sampleSize,
            ActualSampleSize = sample.Length,
            ObservedCandidates = observedCandidates.Length,
            PendingCandidates = observedCandidates.Count(candidate =>
                candidate.Status is
                    OpportunityCandidateStatus.New or
                    OpportunityCandidateStatus.Monitoring),
            SingleChannelMunicipalities = population.Count(item =>
                item.ObservedChannels == 1),
            CoverageGapMunicipalities = population.Count(item =>
                item.HealthyCentralSources == 0 || item.HealthyDirectSources == 0),
            ZeroSignalMunicipalities = population.Count(item =>
                item.ObservedChannels == 0),
            CrossChannelMunicipalities = population.Count(item =>
                item.ObservedChannels >= 2),
            Sample = sample
        };
    }

    private static OpportunityAuditMunicipality BuildMunicipality(
        string municipality,
        MunicipalityOpportunityCoverage? coverage,
        IReadOnlyList<OpportunityCandidate> candidates)
    {
        OpportunityCandidate[] municipalCandidates = candidates
            .Where(candidate => string.Equals(
                candidate.Municipality,
                municipality,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        int central = municipalCandidates.Count(candidate => IsCentral(candidate.SourceKind));
        int direct = municipalCandidates.Count(candidate =>
            candidate.SourceKind == OpportunitySourceKind.MunicipalNoticeBoard);
        int commercial = municipalCandidates.Count(candidate =>
            candidate.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite);
        int observedChannels = Convert.ToInt32(central > 0) +
                               Convert.ToInt32(direct > 0) +
                               Convert.ToInt32(commercial > 0);
        int pending = municipalCandidates.Count(candidate =>
            candidate.Status is
                OpportunityCandidateStatus.New or
                OpportunityCandidateStatus.Monitoring);
        int healthyCentral = coverage?.HealthyCentralSources ?? 0;
        int healthyDirect = coverage?.HealthyDirectSources ?? 0;
        MunicipalityCoverageStatus coverageStatus =
            coverage?.Status ?? MunicipalityCoverageStatus.NotChecked;

        return new()
        {
            Municipality = municipality,
            Reason = ResolveReason(
                observedChannels,
                municipalCandidates.Length,
                pending,
                healthyCentral,
                healthyDirect),
            CoverageStatus = coverageStatus,
            HealthyCentralSources = healthyCentral,
            HealthyDirectSources = healthyDirect,
            CentralCandidates = central,
            DirectCandidates = direct,
            CommercialCandidates = commercial,
            ObservedChannels = observedChannels,
            PendingCandidates = pending
        };
    }

    private static OpportunityAuditReason ResolveReason(
        int observedChannels,
        int candidateCount,
        int pendingCandidates,
        int healthyCentralSources,
        int healthyDirectSources)
    {
        if (observedChannels == 1 && pendingCandidates > 0)
        {
            return OpportunityAuditReason.SingleChannelSignal;
        }

        if (healthyCentralSources == 0 || healthyDirectSources == 0)
        {
            return OpportunityAuditReason.CoverageGap;
        }

        if (candidateCount == 0)
        {
            return OpportunityAuditReason.ZeroSignalControl;
        }

        return observedChannels <= 1
            ? OpportunityAuditReason.SingleChannelSignal
            : OpportunityAuditReason.CrossChannelControl;
    }

    private static bool IsCentral(OpportunitySourceKind sourceKind)
    {
        return sourceKind is
            OpportunitySourceKind.RegionalGazette or
            OpportunitySourceKind.StateGazette or
            OpportunitySourceKind.PublicProcurement or
            OpportunitySourceKind.PublicLandPortal;
    }

    private static bool ObservedWithin(
        OpportunityCandidate candidate,
        DateOnly from,
        DateOnly to)
    {
        DateTimeOffset observedAt = candidate.PublishedAtUtc ?? candidate.LastSeenUtc;
        DateOnly observedDate = DateOnly.FromDateTime(observedAt.UtcDateTime);
        return observedDate >= from && observedDate <= to;
    }

    private static int Priority(OpportunityAuditReason reason)
    {
        return reason switch
        {
            OpportunityAuditReason.SingleChannelSignal => 0,
            OpportunityAuditReason.CoverageGap => 1,
            OpportunityAuditReason.ZeroSignalControl => 2,
            OpportunityAuditReason.CrossChannelControl => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };
    }
}
