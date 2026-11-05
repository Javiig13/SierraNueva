using SierraNueva.Contracts;
using SierraNueva.Core.Discovery;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Tests;

public sealed class CommercialCoverageTests
{
    [Fact]
    public void Coverage_UsesEnabledConfigurationNotDeclaredReviewStatusOrSearchCoverage()
    {
        MarketActorCatalog actors = new()
        {
            Actors =
            [
                new() { Id = "active", OfficialWebsite = "https://www.active.test/", ReviewStatus = MarketActorReviewStatus.Candidate },
                new() { Id = "old", OfficialWebsite = "https://old.test/", ReviewStatus = MarketActorReviewStatus.Monitored, MonitoredSourceIds = ["disabled"] },
                new() { Id = "no-web", Name = "A" },
                new() { Id = "pending", Name = "Z", OfficialWebsite = "https://pending.test/" },
                new() { Id = "rejected", Name = "Rejected", ReviewStatus = MarketActorReviewStatus.Rejected }
            ]
        };
        SourceDefinition[] sources =
        [
            new() { Id = "active", Enabled = true, BaseUrl = "https://active.test/detail", FixedMunicipality = "Loeches" },
            new() { Id = "disabled", Enabled = false, BaseUrl = "https://old.test/", FixedMunicipality = "Madrid" }
        ];
        OpportunityDiscoveryCatalog discovery = new()
        {
            Sources =
            [
                new() { Id = "search", Enabled = true, SourceKind = OpportunitySourceKind.WebSearch, UrlTemplate = "https://search.test/" },
                new() { Id = "new", Enabled = true, SourceKind = OpportunitySourceKind.OfficialCommercialWebsite, UrlTemplate = "https://new.test/sitemap.xml" }
            ]
        };
        MunicipalityDefinition[] municipalities =
        [
            new() { OfficialName = "Madrid", Enabled = true },
            new() { OfficialName = "Loeches", Enabled = true },
            new() { OfficialName = "Disabled", Enabled = false }
        ];
        CommercialCoverageReport report = new CommercialCoverageService().Create(actors, sources, discovery, municipalities, DateTimeOffset.UnixEpoch);
        Assert.Equal(2, report.EnabledMunicipalities);
        Assert.Equal(1, report.MunicipalitiesWithConfiguredExtraction);
        Assert.Equal(1, report.EnabledExtractionSources);
        Assert.Equal(1, report.ActorsWithConfiguredChannel);
        Assert.Equal(1, report.DeclaredMonitoredActors);
        Assert.Equal(2, report.ActorsWithWebsiteAwaitingReview);
        Assert.Equal(1, report.ActorsAwaitingWebsiteResolution);
        Assert.Equal(["old"], report.StaleActorDeclarations);
        Assert.Equal(["new.test"], report.DomainsWithoutActor);
        Assert.Equal(["Madrid"], report.MunicipalitiesWithoutConfiguredExtraction);
        Assert.Equal("no-web", report.ActorsToReview[^1].Id);
        Assert.DoesNotContain(report.ActorsToReview, actor => actor.Id == "rejected");
        Assert.Equal(MarketActorReviewStatus.Candidate, actors.Actors[0].ReviewStatus);
    }
}
