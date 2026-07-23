using SierraNueva.Contracts;
using SierraNueva.Core.Identity;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Core.Tests;

public sealed class NormalizationTests
{
    [Theory]
    [InlineData("  San   LORENZO  ", "san lorenzo")]
    [InlineData("Próxima-fase", "proxima fase")]
    [InlineData("Cerceda / Mataelpino", "cerceda mataelpino")]
    public void NormalizeForComparison_RemovesNoise(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.NormalizeForComparison(input));
    }

    [Fact]
    public void CleanEvidence_TruncatesAndCollapsesWhitespace()
    {
        string input = $"  uno\n dos   {new string('x', 300)} ";

        string result = TextNormalizer.CleanEvidence(input, 40);

        Assert.Equal(40, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeUrl_RemovesTrackingAndNormalizesScheme()
    {
        string normalized = UrlNormalizer.Normalize(
            "http://EXAMPLE.com/promocion/?utm_source=test&b=2&a=1#map");

        Assert.Equal("https://example.com/promocion?a=1&b=2", normalized);
    }

    [Fact]
    public void MunicipalityCatalog_MapsLocalityToOfficialMunicipality()
    {
        MunicipalityCatalog catalog = new(
        [
            new MunicipalityDefinition
            {
                OfficialName = "El Boalo",
                Aliases = ["Boalo"],
                Localities = ["Cerceda", "Mataelpino"]
            }
        ]);

        Assert.Equal("El Boalo", catalog.ResolveOfficialName("Chalets nuevos en Cerceda"));
    }

    [Fact]
    public void PromotionIdentity_IsStableForTrackingVariants()
    {
        Promotion first = new()
        {
            Name = "Residencial Norte",
            Municipality = "Moralzarzal",
            CanonicalUrl = "http://example.com/norte/?utm_campaign=x"
        };
        Promotion second = new()
        {
            Name = "Otro texto",
            Municipality = "Otro",
            CanonicalUrl = "https://example.com/norte"
        };

        Assert.Equal(PromotionIdentity.Create(first), PromotionIdentity.Create(second));
    }
}
