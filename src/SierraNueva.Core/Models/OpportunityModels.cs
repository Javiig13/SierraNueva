namespace SierraNueva.Core.Models;

public enum OpportunitySourceKind
{
    RegionalGazette,
    StateGazette,
    PublicProcurement,
    PublicLandPortal,
    MunicipalNoticeBoard,
    OfficialCommercialWebsite
}

public enum OpportunityFeedFormat
{
    Rss,
    BoeJson,
    Atom,
    ZipAtom,
    Html,
    BocmCalendar,
    EAdminHtml,
    Sitemap
}

public enum OpportunityFeedCadence
{
    Once,
    Daily,
    Monthly
}

public enum OpportunityKind
{
    LandDisposal,
    SurfaceRight,
    Planning,
    UrbanizationWorks,
    BuildingPermit,
    ResidentialDevelopment,
    Other
}

public enum OpportunityCandidateStatus
{
    New,
    Monitoring,
    Rejected,
    VerifiedSource,
    Stale
}

public enum OpportunitySourceHealthStatus
{
    NotChecked,
    Healthy,
    Degraded,
    Failing
}

public enum MunicipalityCoverageStatus
{
    NotChecked,
    CentralOnly,
    DirectOnly,
    DirectAndCentral,
    Degraded
}

public sealed class OpportunityTermRule
{
    public string Term { get; init; } = string.Empty;

    public OpportunityKind Kind { get; init; }
}

public sealed class OpportunitySourceDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public OpportunitySourceKind SourceKind { get; init; }

    public OpportunityFeedFormat Format { get; init; }

    public OpportunityFeedCadence Cadence { get; init; }

    public string? UrlTemplate { get; init; }

    public string? FixturePath { get; init; }

    public IReadOnlyList<string> AllowedHosts { get; init; } = [];

    public IReadOnlyList<string> ItemSelectors { get; init; } = [];

    public string? FixedMunicipality { get; init; }

    public int MaxItems { get; init; } = 2_000;
}

public sealed class OpportunityDiscoveryCatalog
{
    public string SchemaVersion { get; init; } = "1.0";

    public int DefaultLookbackDays { get; init; } = 7;

    public IReadOnlyList<OpportunityTermRule> Terms { get; init; } = [];

    public IReadOnlyList<string> ContextTerms { get; init; } = [];

    public IReadOnlyList<string> ExclusionTerms { get; init; } = [];

    public IReadOnlyList<OpportunitySourceDefinition> Sources { get; init; } = [];
}

public sealed class OpportunityFeedItem
{
    public string ExternalId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string OfficialUrl { get; init; } = string.Empty;

    public DateTimeOffset? PublishedAtUtc { get; init; }
}

public sealed class OpportunityCandidate
{
    public string Id { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public OpportunitySourceKind SourceKind { get; init; }

    public string ExternalId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string OfficialUrl { get; init; } = string.Empty;

    public DateTimeOffset? PublishedAtUtc { get; init; }

    public string Municipality { get; init; } = string.Empty;

    public OpportunityKind Kind { get; init; }

    public decimal Confidence { get; init; }

    public IReadOnlyList<string> MatchedTerms { get; init; } = [];

    public DateTimeOffset FirstSeenUtc { get; init; }

    public DateTimeOffset LastSeenUtc { get; init; }

    public OpportunityCandidateStatus Status { get; init; } =
        OpportunityCandidateStatus.New;
}

public sealed class OpportunitySourceRun
{
    public string SourceId { get; init; } = string.Empty;

    public bool Success { get; init; }

    public int ItemsRead { get; init; }

    public int CandidatesMatched { get; init; }

    public string? Error { get; init; }
}

public sealed class OpportunityDiscoveryRun
{
    public string RunId { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public int NewCandidates { get; init; }

    public int UpdatedCandidates { get; init; }

    public IReadOnlyList<OpportunitySourceRun> Sources { get; init; } = [];
}

public sealed class OpportunitySourceHealth
{
    public string SourceId { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public OpportunitySourceKind SourceKind { get; init; }

    public OpportunityFeedCadence Cadence { get; init; }

    public string? FixedMunicipality { get; init; }

    public DateTimeOffset? FirstCheckedUtc { get; init; }

    public DateTimeOffset? LastAttemptUtc { get; init; }

    public DateTimeOffset? LastSuccessUtc { get; init; }

    public DateTimeOffset? LastFailureUtc { get; init; }

    public DateTimeOffset? LastNonEmptyUtc { get; init; }

    public DateTimeOffset? NextCheckDueUtc { get; init; }

    public int ConsecutiveFailures { get; init; }

    public int ConsecutiveEmptyRuns { get; init; }

    public int LastItemsRead { get; init; }

    public int LastCandidatesMatched { get; init; }

    public OpportunitySourceHealthStatus Status { get; init; } =
        OpportunitySourceHealthStatus.NotChecked;

    public IReadOnlyList<string> Issues { get; init; } = [];
}

public sealed class MunicipalityOpportunityCoverage
{
    public string Municipality { get; init; } = string.Empty;

    public MunicipalityCoverageStatus Status { get; init; }

    public int ConfiguredDirectSources { get; init; }

    public int HealthyDirectSources { get; init; }

    public int HealthyCentralSources { get; init; }

    public DateTimeOffset? LastSuccessfulCheckUtc { get; init; }

    public int NewCandidates { get; init; }

    public int MonitoringCandidates { get; init; }
}

public sealed class OpportunityCoverageSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public int EnabledSources { get; init; }

    public int HealthySources { get; init; }

    public int DegradedSources { get; init; }

    public int FailingSources { get; init; }

    public int MunicipalitiesTotal { get; init; }

    public int MunicipalitiesWithDirectSource { get; init; }

    public int MunicipalitiesWithHealthyDirectSource { get; init; }

    public int MunicipalitiesWithHealthyCoverage { get; init; }

    public int PendingCandidates { get; init; }

    public IReadOnlyList<MunicipalityOpportunityCoverage> Municipalities { get; init; } = [];
}

public sealed class OpportunityRadarState
{
    public string SchemaVersion { get; init; } = "1.0";

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public OpportunityDiscoveryRun? LastRun { get; init; }

    public IReadOnlyList<OpportunityCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<OpportunitySourceHealth> SourceHealth { get; init; } = [];

    public OpportunityCoverageSnapshot Coverage { get; init; } = new();
}

public sealed class OpportunityDiscoveryRequest
{
    public OpportunityDiscoveryCatalog Catalog { get; init; } = new();

    public IReadOnlyList<Contracts.MunicipalityDefinition> Municipalities { get; init; } = [];

    public string StateDirectory { get; init; } = string.Empty;

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public string? SourceFilter { get; init; }

    public IReadOnlyList<string> KnownPromotionUrls { get; init; } = [];

    public bool DryRun { get; init; }
}

public sealed class OpportunityDiscoveryResult
{
    public OpportunityRadarState State { get; init; } = new();

    public OpportunityDiscoveryRun Run { get; init; } = new();
}
