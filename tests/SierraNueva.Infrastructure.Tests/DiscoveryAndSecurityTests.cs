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
}
