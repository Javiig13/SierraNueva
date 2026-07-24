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

    [Fact]
    public void Triage_PrioritizesSignalsAndMarksProbableDuplicatesWithoutChangingState()
    {
        DateTimeOffset timestamp = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        OpportunityCandidate direct = Candidate(
            "Galapagar",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.New,
            timestamp.AddDays(-2),
            "Residencial Sierra Galapagar, chalets de obra nueva",
            "https://promotora.example/residencial-sierra",
            0.65m,
            ["obra nueva", "chalets"]);
        OpportunityCandidate duplicate = Candidate(
            "Galapagar",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.New,
            timestamp.AddDays(-1),
            direct.Title,
            "https://otra.example/residencial-sierra",
            0.45m,
            ["obra nueva"]);
        OpportunityCandidate administrative = Candidate(
            "Alpedrete",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.Monitoring,
            timestamp.AddDays(-5),
            "Aprobación del plan parcial del sector norte de Alpedrete",
            "https://sede.madrid.org/plan-parcial",
            0.35m,
            ["plan parcial"]);
        OpportunityCandidate low = Candidate(
            "Moralzarzal",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.New,
            timestamp.AddDays(-100),
            "Actualidad local",
            "https://medio.example/noticia",
            0.45m);
        OpportunityCandidate rejected = Candidate(
            "Moralzarzal",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.Rejected,
            timestamp,
            "Promoción descartada",
            "https://noise.example/item",
            0.9m);
        OpportunityRadarState state = new()
        {
            UpdatedAtUtc = timestamp.AddMinutes(-1),
            Candidates = [direct, duplicate, administrative, low, rejected]
        };

        OpportunityTriageReport report = new OpportunityTriageService().Create(
            state,
            timestamp);

        Assert.Equal(4, report.PendingCandidates);
        Assert.Equal(1, report.HighPriority);
        Assert.Equal(1, report.MediumPriority);
        Assert.Equal(1, report.LowPriority);
        Assert.Equal(1, report.PossibleDuplicates);
        OpportunityTriageItem directItem = Assert.Single(
            report.Items,
            item => item.CandidateId == direct.Id);
        Assert.Equal(OpportunityTriageBand.DirectPromotion, directItem.Band);
        Assert.Equal(OpportunityTriagePriority.High, directItem.Priority);
        Assert.Contains(
            OpportunityTriageReason.MunicipalityInTitleOrUrl,
            directItem.Reasons);
        OpportunityTriageItem administrativeItem = Assert.Single(
            report.Items,
            item => item.CandidateId == administrative.Id);
        Assert.Equal(
            OpportunityTriageBand.AdministrativeSignal,
            administrativeItem.Band);
        Assert.Contains(
            OpportunityTriageReason.PublicAdministrationHost,
            administrativeItem.Reasons);
        OpportunityTriageItem duplicateItem = Assert.Single(
            report.Items,
            item => item.CandidateId == duplicate.Id);
        Assert.Equal(OpportunityTriagePriority.Duplicate, duplicateItem.Priority);
        Assert.Equal(direct.Id, duplicateItem.DuplicateOfCandidateId);
        Assert.DoesNotContain(
            report.Items,
            item => item.CandidateId == rejected.Id);
        Assert.Equal(OpportunityCandidateStatus.New, direct.Status);
        Assert.Equal(OpportunityCandidateStatus.New, duplicate.Status);
        Assert.Equal(OpportunityCandidateStatus.Monitoring, administrative.Status);
        Assert.Equal(OpportunityCandidateStatus.Rejected, rejected.Status);
    }

    [Fact]
    public void Triage_DemotesUnconfirmedHistoricalAndExcludedSearchResults()
    {
        DateTimeOffset timestamp = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        OpportunityCandidate summaryOnly = Candidate(
            "Bustarviejo",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.New,
            null,
            "Aife Mairena, 66 viviendas unifamiliares de obra nueva en Sevilla",
            "https://promotora.example/aife-mairena",
            0.55m,
            ["obra nueva", "viviendas unifamiliares"],
            summary: "Catálogo nacional de promociones con enlace a Bustarviejo.",
            omitPublishedAt: true);
        OpportunityCandidate excludedListing = Candidate(
            "Galapagar",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.New,
            null,
            "Obra nueva en Galapagar",
            "https://pisos.nestoria.es/galapagar/obra-nueva",
            0.55m,
            ["obra nueva", "promoción"],
            omitPublishedAt: true);
        OpportunityCandidate historicalForm = Candidate(
            "Alpedrete",
            OpportunitySourceKind.WebSearch,
            OpportunityCandidateStatus.New,
            null,
            "Ordenanza fiscal y solicitud de licencia de obra",
            "https://www.alpedrete.es/documentos/2023/ordenanza.pdf",
            0.45m,
            ["licencia de obra", "promoción"],
            omitPublishedAt: true);
        OpportunityRadarState state = new()
        {
            UpdatedAtUtc = timestamp.AddMinutes(-1),
            Candidates = [summaryOnly, excludedListing, historicalForm]
        };

        OpportunityTriageReport report = new OpportunityTriageService().Create(
            state,
            timestamp,
            ["nestoria.es"]);

        OpportunityTriageItem summaryItem = Assert.Single(
            report.Items,
            item => item.CandidateId == summaryOnly.Id);
        Assert.Equal(OpportunityTriagePriority.Medium, summaryItem.Priority);
        Assert.Contains(
            OpportunityTriageReason.MunicipalityOnlyInSummary,
            summaryItem.Reasons);
        OpportunityTriageItem listingItem = Assert.Single(
            report.Items,
            item => item.CandidateId == excludedListing.Id);
        Assert.Equal(OpportunityTriagePriority.Low, listingItem.Priority);
        Assert.Contains(
            OpportunityTriageReason.ExcludedListingHost,
            listingItem.Reasons);
        OpportunityTriageItem formItem = Assert.Single(
            report.Items,
            item => item.CandidateId == historicalForm.Id);
        Assert.Equal(OpportunityTriagePriority.Low, formItem.Priority);
        Assert.Contains(
            OpportunityTriageReason.HistoricalReference,
            formItem.Reasons);
        Assert.Contains(
            OpportunityTriageReason.GenericAdministrativeReference,
            formItem.Reasons);
    }

    private static OpportunityCandidate Candidate(
        string municipality,
        OpportunitySourceKind sourceKind,
        OpportunityCandidateStatus status,
        DateTimeOffset? publishedAtUtc = null,
        string title = "",
        string officialUrl = "",
        decimal confidence = 0m,
        IReadOnlyList<string>? matchedTerms = null,
        string summary = "",
        bool omitPublishedAt = false)
    {
        return new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Municipality = municipality,
            SourceKind = sourceKind,
            Title = title,
            Summary = summary,
            OfficialUrl = officialUrl,
            Confidence = confidence,
            MatchedTerms = matchedTerms ?? [],
            FirstSeenUtc = publishedAtUtc ??
                           new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
            LastSeenUtc = publishedAtUtc ??
                          new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
            PublishedAtUtc = omitPublishedAt
                ? null
                : publishedAtUtc ??
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
