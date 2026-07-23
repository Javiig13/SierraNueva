using System.Net;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;

namespace SierraNueva.Infrastructure.Security;

public sealed class BlocklistUrlPolicy(DomainExclusions exclusions) : IUrlPolicy
{
    private readonly HashSet<string> _blocked = exclusions.Domains
        .Select(domain => domain.Trim().TrimStart('.').ToLowerInvariant())
        .Where(domain => domain.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(Uri url, SourceDefinition source, out SkipReason reason)
    {
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            reason = SkipReason.UnsupportedScheme;
            return false;
        }

        string host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (_blocked.Any(domain =>
                host == domain || host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)))
        {
            reason = SkipReason.BlockedDomain;
            return false;
        }

        if (host is "localhost" or "localhost.localdomain" ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            IsPrivateAddress(host))
        {
            reason = SkipReason.PrivateNetwork;
            return false;
        }

        if (source.AllowedHosts.Count > 0 &&
            !source.AllowedHosts.Any(allowed =>
                host == allowed ||
                host.EndsWith(
                    $".{allowed.TrimStart('.')}",
                    StringComparison.OrdinalIgnoreCase)))
        {
            reason = SkipReason.BlockedDomain;
            return false;
        }

        reason = SkipReason.None;
        return true;
    }

    private static bool IsPrivateAddress(string host)
    {
        if (!IPAddress.TryParse(host, out IPAddress? address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   bytes[0] == 0 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast;
    }
}
