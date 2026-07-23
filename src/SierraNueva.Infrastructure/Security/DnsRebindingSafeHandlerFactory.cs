using System.Net;
using System.Net.Sockets;

namespace SierraNueva.Infrastructure.Security;

public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(
        string host,
        CancellationToken cancellationToken);
}

public sealed class SystemDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        return Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}

public sealed class DnsRebindingSafeHandlerFactory(IDnsResolver dnsResolver)
{
    public SocketsHttpHandler Create(
        int maxAutomaticRedirections,
        bool useSessionCookies = false)
    {
        return new()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = maxAutomaticRedirections,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = useSessionCookies,
            CookieContainer = new CookieContainer(),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = ConnectAsync
        };
    }

    public async Task<IReadOnlyList<IPAddress>> ResolvePublicAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await dnsResolver.GetHostAddressesAsync(
            host,
            cancellationToken);
        if (addresses.Length == 0)
        {
            throw new HttpRequestException($"DNS no devolvió direcciones para '{host}'.");
        }

        IPAddress[] distinct = addresses.Distinct().ToArray();
        IPAddress? unsafeAddress = distinct.FirstOrDefault(address =>
            !NetworkAddressPolicy.IsPublic(address));
        if (unsafeAddress is not null)
        {
            throw new HttpRequestException(
                $"La resolución DNS de '{host}' contiene una dirección no pública.");
        }

        return distinct;
    }

    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IPAddress> addresses = await ResolvePublicAddressesAsync(
            context.DnsEndPoint.Host,
            cancellationToken);
        List<Exception> failures = [];
        foreach (IPAddress address in addresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (
                exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                failures.Add(exception);
                if (exception is OperationCanceledException)
                {
                    throw;
                }
            }
        }

        throw new HttpRequestException(
            $"No se pudo conectar a ninguna dirección validada de '{context.DnsEndPoint.Host}'.",
            new AggregateException(failures));
    }
}
