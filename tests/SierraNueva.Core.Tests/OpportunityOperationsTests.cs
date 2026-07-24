using SierraNueva.Contracts;
using SierraNueva.Core.Discovery;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Tests;

public sealed class OpportunityOperationsTests
{
    [Fact]
    public void BackfillPlanner_SplitsLongRangesWithoutGapsOrOverlaps()
    {
        DateOnly from = new(2024, 1, 1);
        DateOnly to = new(2025, 12, 31);

        IReadOnlyList<OpportunityBackfillBatch> batches =
            OpportunityBackfillPlanner.Plan(from, to);

        Assert.Equal(2, batches.Count);
        Assert.Equal(from, batches[0].From);
        Assert.Equal(new DateOnly(2025, 1, 1), batches[0].To);
        Assert.Equal(new DateOnly(2025, 1, 2), batches[1].From);
        Assert.Equal(to, batches[1].To);
        Assert.All(
            batches,
            batch => Assert.InRange(
                batch.To.DayNumber - batch.From.DayNumber + 1,
                1,
                OpportunityBackfillPlanner.MaximumBatchDays));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(368)]
    public void BackfillPlanner_RejectsUnsafeBatchSizes(int batchDays)
    {
        Assert.Throws<InvalidDataException>(() => OpportunityBackfillPlanner.Plan(
            new(2026, 1, 1),
            new(2026, 1, 2),
            batchDays));
    }

    [Fact]
    public void Audit_PrioritizesSingleChannelSignalsAndCoverageGaps()
    {
        DateTimeOffset timestamp = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        MunicipalityDefinition[] municipalities =
        [
            new() { OfficialName = "Alpedrete", Enabled = true },
            new() { OfficialName = "Galapagar", Enabled = true },
            new() { OfficialName = "Robledo de Chavela", Enabled = true }
        ];
        OpportunityRadarState state = new()
        {
            UpdatedAtUtc = timestamp.AddMinutes(-5),
            Candidates =
            [
                Candidate(
                    "Alpedrete",
                    OpportunitySourceKind.RegionalGazette,
                    OpportunityCandidateStatus.New),
                Candidate(
                    "Galapagar",
                    OpportunitySourceKind.MunicipalNoticeBoard,
                    OpportunityCandidateStatus.VerifiedSource),
                Candidate(
                    "Galapagar",
                    OpportunitySourceKind.OfficialCommercialWebsite,
                    OpportunityCandidateStatus.VerifiedSource),
                Candidate(
                    "Robledo de Chavela",
                    OpportunitySourceKind.RegionalGazette,
                    OpportunityCandidateStatus.New,
                    new(2026, 6, 20, 10, 0, 0, TimeSpan.Zero))
            ],
            Coverage = new()
            {
                GeneratedAtUtc = timestamp.AddMinutes(-5),
                Municipalities =
                [
                    Coverage("Alpedrete", healthyDirect: 1, healthyCentral: 2),
                    Coverage("Galapagar", healthyDirect: 1, healthyCentral: 2),
                    Coverage("Robledo de Chavela", healthyDirect: 0, healthyCentral: 2)
                ]
            }
        };

        OpportunityAuditReport report = new OpportunityAuditService().Create(
            state,
            municipalities,
            sampleSize: 3,
            new(2026, 7, 1),
            new(2026, 7, 31),
            timestamp);

        Assert.Equal(3, report.Population);
        Assert.Equal(3, report.ObservedCandidates);
        Assert.Equal(1, report.PendingCandidates);
        Assert.Equal(1, report.SingleChannelMunicipalities);
        Assert.Equal(1, report.CoverageGapMunicipalities);
        Assert.Equal(1, report.ZeroSignalMunicipalities);
        Assert.Equal(1, report.CrossChannelMunicipalities);
        Assert.Equal(
            ["Alpedrete", "Robledo de Chavela", "Galapagar"],
            report.Sample.Select(item => item.Municipality).ToArray());
        Assert.Equal(
            OpportunityAuditReason.SingleChannelSignal,
            report.Sample[0].Reason);
        Assert.Equal(
            OpportunityAuditReason.CoverageGap,
            report.Sample[1].Reason);
    }

    [Fact]
    public void Audit_ExcludesRejectedAndStaleCandidatesFromObservedSignals()
    {
        MunicipalityDefinition municipality = new()
        {
            OfficialName = "Moralzarzal",
            Enabled = true
        };
        OpportunityRadarState state = new()
        {
            UpdatedAtUtc = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero),
            Candidates =
            [
                Candidate(
                    municipality.OfficialName,
                    OpportunitySourceKind.RegionalGazette,
                    OpportunityCandidateStatus.Rejected),
                Candidate(
                    municipality.OfficialName,
                    OpportunitySourceKind.MunicipalNoticeBoard,
                    OpportunityCandidateStatus.Stale)
            ],
            Coverage = new()
            {
                Municipalities = [Coverage(municipality.OfficialName, 1, 1)]
            }
        };

        OpportunityAuditReport report = new OpportunityAuditService().Create(
            state,
            [municipality],
            1,
            new(2026, 7, 1),
            new(2026, 7, 31),
            state.UpdatedAtUtc);

        Assert.Equal(0, report.ObservedCandidates);
        Assert.Equal(1, report.ZeroSignalMunicipalities);
        Assert.Equal(OpportunityAuditReason.ZeroSignalControl, report.Sample[0].Reason);
    }

    private static OpportunityCandidate Candidate(
        string municipality,
        OpportunitySourceKind sourceKind,
        OpportunityCandidateStatus status,
        DateTimeOffset? publishedAtUtc = null)
    {
        return new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Municipality = municipality,
            SourceKind = sourceKind,
            PublishedAtUtc = publishedAtUtc ??
                             new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
            Status = status
        };
    }

    private static MunicipalityOpportunityCoverage Coverage(
        string municipality,
        int healthyDirect,
        int healthyCentral)
    {
        return new()
        {
            Municipality = municipality,
            Status = healthyDirect > 0
                ? MunicipalityCoverageStatus.DirectAndCentral
                : MunicipalityCoverageStatus.CentralOnly,
            HealthyDirectSources = healthyDirect,
            HealthyCentralSources = healthyCentral
        };
    }
}
