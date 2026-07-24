using SierraNueva.Contracts;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Abstractions;

public interface IPageSource
{
    Task<PageBatch> FetchAsync(
        SourceDefinition source,
        CrawlerSettings settings,
        int? maxPagesOverride,
        bool disablePlaywright,
        CancellationToken cancellationToken);
}

public interface IUrlDiscoveryProvider
{
    Task<IReadOnlyList<Uri>> DiscoverAsync(
        SourceDefinition source,
        CancellationToken cancellationToken);
}

public interface IPromotionExtractor
{
    Task<IReadOnlyList<Promotion>> ExtractAsync(
        FetchedPage page,
        SourceDefinition source,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        CancellationToken cancellationToken);
}

public interface IGeocoder
{
    Task<Promotion> GeocodeAsync(
        Promotion promotion,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        CancellationToken cancellationToken);
}

public interface IPromotionStateRepository
{
    Task<IReadOnlyList<Promotion>> LoadAsync(string stateDirectory, CancellationToken cancellationToken);

    Task SaveAsync(
        string stateDirectory,
        IReadOnlyList<Promotion> promotions,
        CancellationToken cancellationToken);
}

public interface IEnrichmentStateRepository
{
    Task<EnrichmentState> LoadAsync(
        string stateDirectory,
        CancellationToken cancellationToken);

    Task SaveAsync(
        string stateDirectory,
        EnrichmentState queue,
        CancellationToken cancellationToken);
}

public interface IPromotionEnrichmentProvider
{
    string ProviderName { get; }

    string Model { get; }

    Task<IReadOnlyList<EnrichmentFieldProposal>> ProposeAsync(
        EnrichmentEvidenceDocument evidence,
        IReadOnlyList<string> missingFields,
        CancellationToken cancellationToken);
}

public interface IPublicDataWriter
{
    Task WriteAsync(
        string outputDirectory,
        PromotionDataset dataset,
        ChangeDataset changes,
        RunReport run,
        CancellationToken cancellationToken);
}

public interface IUrlPolicy
{
    bool IsAllowed(Uri url, SourceDefinition source, out SkipReason reason);
}

public interface IPdfTextExtractor
{
    string Extract(ReadOnlyMemory<byte> content);
}

public interface IDynamicPageRenderer
{
    Task<string?> RenderAsync(Uri url, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IOpportunityFeedReader
{
    Task<IReadOnlyList<OpportunityFeedItem>> ReadAsync(
        OpportunitySourceDefinition source,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}

public interface IOpportunityStateRepository
{
    Task<OpportunityRadarState> LoadAsync(
        string stateDirectory,
        CancellationToken cancellationToken);

    Task SaveAsync(
        string stateDirectory,
        OpportunityRadarState state,
        CancellationToken cancellationToken);
}
