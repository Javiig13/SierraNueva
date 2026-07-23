namespace SierraNueva.Contracts;

public enum LocationPrecision
{
    Unknown,
    MunicipalityCentroid,
    Locality,
    DevelopmentArea,
    Street,
    ExactCoordinates
}

public enum SourceKind
{
    Unknown,
    OfficialPromoter,
    OfficialMicrosite,
    CooperativeManager,
    ExclusiveMarketer,
    Builder,
    PublicAuthority
}

public enum CommercialStatus
{
    Unknown,
    Announced,
    Upcoming,
    PreSales,
    OnSale,
    LastUnits,
    SoldOut,
    Completed,
    Paused
}

public enum ConstructionStatus
{
    Unknown,
    Planned,
    Licensed,
    UnderConstruction,
    Completed
}

public enum FieldQuality
{
    Unknown,
    Explicit,
    Normalized,
    Inferred,
    Approximate
}

public enum ChangeKind
{
    Added,
    Updated,
    Deactivated,
    Reactivated
}

public enum RunStatus
{
    Success,
    PartialSuccess,
    Failed,
    ValidationFailed
}

public enum SourceRunStatus
{
    Success,
    Skipped,
    Failed
}

public enum SkipReason
{
    None,
    Disabled,
    BlockedDomain,
    RobotsDenied,
    UnsupportedScheme,
    PrivateNetwork,
    InvalidContentType,
    ResponseTooLarge,
    DuplicateUrl,
    IrrelevantUrl,
    FetchFailed
}
