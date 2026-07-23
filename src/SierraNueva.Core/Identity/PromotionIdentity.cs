using System.Security.Cryptography;
using System.Text;
using SierraNueva.Contracts;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Core.Identity;

public static class PromotionIdentity
{
    public static string Create(Promotion promotion)
    {
        string identity = !string.IsNullOrWhiteSpace(promotion.CanonicalUrl)
            ? $"url|{UrlNormalizer.Normalize(promotion.CanonicalUrl)}"
            : BuildCompositeIdentity(promotion);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"sn-{Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant()}";
    }

    public static string Fingerprint(Promotion promotion)
    {
        return string.Join(
            "|",
            TextNormalizer.NormalizeForComparison(promotion.Name),
            TextNormalizer.NormalizeForComparison(promotion.Municipality),
            TextNormalizer.NormalizeCompanyName(promotion.DeveloperName));
    }

    private static string BuildCompositeIdentity(Promotion promotion)
    {
        string normalizedName = TextNormalizer.NormalizeForComparison(promotion.Name);
        string municipality = TextNormalizer.NormalizeForComparison(promotion.Municipality);
        string developer = TextNormalizer.NormalizeCompanyName(promotion.DeveloperName);
        if (normalizedName.Length > 0 && municipality.Length > 0)
        {
            return $"name|{normalizedName}|{municipality}|{developer}";
        }

        return $"place|{TextNormalizer.NormalizeForComparison(promotion.Address ?? promotion.Locality)}|{municipality}|{developer}";
    }
}
