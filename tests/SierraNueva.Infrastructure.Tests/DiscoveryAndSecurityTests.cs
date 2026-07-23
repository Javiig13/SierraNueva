using System.Net;
using SierraNueva.Contracts;
using SierraNueva.Infrastructure.Crawling;
using SierraNueva.Infrastructure.Discovery;
using SierraNueva.Infrastructure.Security;

namespace SierraNueva.Infrastructure.Tests;

public sealed class DiscoveryAndSecurityTests
{
    [Fact]
    public void RobotsParser_AppliesWildcardRules()
    {
        RobotsRules rules = RobotsRules.Parse(
            """
            User-agent: *
            Disallow: /privado
            Sitemap: https://example.com/sitemap.xml
            """,
            new("https://example.com"));

        Assert.False(rules.IsAllowed(new("https://example.com/privado/ficha")));
        Assert.True(rules.IsAllowed(new("https://example.com/promociones")));
        Assert.Single(rules.Sitemaps);
    }

    [Fact]
    public void SitemapParser_ReadsUrlSetAndIndex()
    {
        IReadOnlyList<Uri> urls = SitemapDiscoveryProvider.ReadLocations(
            """
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <sitemap><loc>https://example.com/one.xml</loc></sitemap>
              <sitemap><loc>/two.xml</loc></sitemap>
            </sitemapindex>
            """,
            new("https://example.com/root.xml"));

        Assert.Equal(2, urls.Count);
        Assert.Equal("https://example.com/two.xml", urls[1].AbsoluteUri);
    }

    [Theory]
    [InlineData("https://idealista.com/obra-nueva", SkipReason.BlockedDomain)]
    [InlineData("http://127.0.0.1/admin", SkipReason.PrivateNetwork)]
    [InlineData("ftp://example.com/file", SkipReason.UnsupportedScheme)]
    public void UrlPolicy_BlocksDangerousOrExcludedUrls(string url, SkipReason expected)
    {
        BlocklistUrlPolicy policy = new(new DomainExclusions
        {
            Domains = ["idealista.com"]
        });
        SourceDefinition source = new();

        bool allowed = policy.IsAllowed(new(url), source, out SkipReason reason);

        Assert.False(allowed);
        Assert.Equal(expected, reason);
    }

    [Fact]
    public void UrlPolicy_RequiresConfiguredHost()
    {
        BlocklistUrlPolicy policy = new(new DomainExclusions());
        SourceDefinition source = new() { AllowedHosts = ["promotora.example"] };

        bool allowed = policy.IsAllowed(
            new("https://other.example/promocion"),
            source,
            out SkipReason reason);

        Assert.False(allowed);
        Assert.Equal(SkipReason.BlockedDomain, reason);
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("192.168.1.20", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("203.0.113.10", false)]
    [InlineData("::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("2001:db8::1", false)]
    public void NetworkAddressPolicy_AllowsOnlyPublicAddresses(
        string value,
        bool expected)
    {
        Assert.Equal(expected, NetworkAddressPolicy.IsPublic(IPAddress.Parse(value)));
    }

    [Fact]
    public async Task DnsSafeHandler_RejectsMixedPublicAndPrivateResolution()
    {
        DnsRebindingSafeHandlerFactory factory = new(new StubDnsResolver(
            IPAddress.Parse("8.8.8.8"),
            IPAddress.Loopback));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => factory.ResolvePublicAddressesAsync(
                "rebind.example",
                CancellationToken.None));

        Assert.Contains("dirección no pública", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(factory.Create(5).ConnectCallback);
    }

    private sealed class StubDnsResolver(params IPAddress[] addresses) : IDnsResolver
    {
        public Task<IPAddress[]> GetHostAddressesAsync(
            string host,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(addresses);
        }
    }
}
