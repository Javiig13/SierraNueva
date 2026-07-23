using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;

namespace SierraNueva.Infrastructure.Discovery;

public sealed class ConfiguredUrlDiscoveryProvider : IUrlDiscoveryProvider
{
    public Task<IReadOnlyList<Uri>> DiscoverAsync(
        SourceDefinition source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Uri> urls = source.StartUrls
            .Select(value => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null)
            .Where(uri => uri is not null)
            .Cast<Uri>()
            .ToArray();
        return Task.FromResult(urls);
    }
}
