using System.Net;
using System.Net.Sockets;

namespace SierraNueva.Infrastructure.Security;

public static class NetworkAddressPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] is > 0 and < 224 &&
                   bytes[0] != 10 &&
                   bytes[0] != 127 &&
                   !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                   !(bytes[0] == 169 && bytes[1] == 254) &&
                   !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                   !(bytes[0] == 192 && bytes[1] is 0 or 168) &&
                   !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
                   !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                   !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) ||
            IPAddress.IsLoopback(address) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast ||
            address.IsIPv6SiteLocal)
        {
            return false;
        }

        byte[] ipv6 = address.GetAddressBytes();
        bool uniqueLocal = (ipv6[0] & 0xfe) == 0xfc;
        bool documentation = ipv6[0] == 0x20 &&
                             ipv6[1] == 0x01 &&
                             ipv6[2] == 0x0d &&
                             ipv6[3] == 0xb8;
        return !uniqueLocal && !documentation;
    }
}
