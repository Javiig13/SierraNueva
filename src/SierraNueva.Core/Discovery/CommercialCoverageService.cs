using SierraNueva.Contracts;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Discovery;

public sealed class CommercialCoverageReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public int EnabledMunicipalities { get; init; }
    public int MunicipalitiesWithConfiguredExtraction { get; init; }
    public int EnabledExtractionSources { get; init; }
    public int RegisteredActors { get; init; }
    public int DeclaredMonitoredActors { get; init; }
    public int ActorsWithConfiguredChannel { get; init; }
    public int ActorsWithWebsiteAwaitingReview { get; init; }
    public int ActorsAwaitingWebsiteResolution { get; init; }
    public IReadOnlyList<string> DomainsWithoutActor { get; init; } = [];
    public IReadOnlyList<string> StaleActorDeclarations { get; init; } = [];
    public IReadOnlyList<string> MunicipalitiesWithoutConfiguredExtraction { get; init; } = [];
    public IReadOnlyList<MarketActorDefinition> ActorsToReview { get; init; } = [];
}

public sealed class CommercialCoverageService
{
    // Configuration coverage is not source health, market share or a legal admission.
    public CommercialCoverageReport Create(
        MarketActorCatalog actors,
        IReadOnlyCollection<SourceDefinition> sources,
        OpportunityDiscoveryCatalog discovery,
        IReadOnlyCollection<MunicipalityDefinition> municipalities,
        DateTimeOffset generatedAtUtc)
    {
        SourceDefinition[] enabled = sources.Where(source => source.Enabled).ToArray();
        HashSet<string> configuredIds = enabled.Select(source => source.Id)
            .Concat(discovery.Sources.Where(source => source.Enabled &&
                source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite)
                .Select(source => source.Id)).ToHashSet(StringComparer.Ordinal);
        HashSet<string> domains = enabled.Select(source => Host(source.BaseUrl))
            .Concat(discovery.Sources.Where(source => source.Enabled &&
                source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite)
                .Select(source => Host(source.UrlTemplate)))
            .Where(host => host.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> actorDomains = actors.Actors.Select(actor => Host(actor.OfficialWebsite))
            .Where(host => host.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] missingMunicipalities = municipalities.Where(municipality => municipality.Enabled)
            .Where(municipality => !enabled.Any(source => string.Equals(
                source.FixedMunicipality, municipality.OfficialName, StringComparison.OrdinalIgnoreCase)))
            .Select(municipality => municipality.OfficialName).Order(StringComparer.Ordinal).ToArray();
        MarketActorDefinition[] backlog = actors.Actors
            .Where(actor => actor.ReviewStatus != MarketActorReviewStatus.Rejected &&
                !domains.Contains(Host(actor.OfficialWebsite)))
            .OrderBy(actor => string.IsNullOrWhiteSpace(actor.OfficialWebsite))
            .ThenBy(actor => actor.Name, StringComparer.Ordinal)
            .ThenBy(actor => actor.Id, StringComparer.Ordinal).ToArray();
        return new()
        {
            GeneratedAtUtc = generatedAtUtc,
            EnabledMunicipalities = municipalities.Count(municipality => municipality.Enabled),
            MunicipalitiesWithConfiguredExtraction = municipalities.Count(municipality => municipality.Enabled) -
                missingMunicipalities.Length,
            EnabledExtractionSources = enabled.Length,
            RegisteredActors = actors.Actors.Count,
            DeclaredMonitoredActors = actors.Actors.Count(actor => actor.ReviewStatus == MarketActorReviewStatus.Monitored),
            ActorsWithConfiguredChannel = actors.Actors.Count(actor => domains.Contains(Host(actor.OfficialWebsite))),
            ActorsWithWebsiteAwaitingReview = backlog.Count(actor => !string.IsNullOrWhiteSpace(actor.OfficialWebsite)),
            ActorsAwaitingWebsiteResolution = backlog.Count(actor => string.IsNullOrWhiteSpace(actor.OfficialWebsite)),
            DomainsWithoutActor = domains.Except(actorDomains, StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal).ToArray(),
            StaleActorDeclarations = actors.Actors.Where(actor =>
                    actor.MonitoredSourceIds.Any(id => !configuredIds.Contains(id)) ||
                    (actor.ReviewStatus == MarketActorReviewStatus.Rejected && domains.Contains(Host(actor.OfficialWebsite))) ||
                    (actor.ReviewStatus == MarketActorReviewStatus.Monitored &&
                     !domains.Contains(Host(actor.OfficialWebsite))))
                .Select(actor => actor.Id).Order(StringComparer.Ordinal).ToArray(),
            MunicipalitiesWithoutConfiguredExtraction = missingMunicipalities,
            ActorsToReview = backlog
        };
    }

    private static string Host(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https")
        {
            return string.Empty;
        }

        string host = uri.IdnHost.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }
}
