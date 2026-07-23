using SierraNueva.Contracts;
using SierraNueva.Core.Normalization;

namespace SierraNueva.Core.Identity;

public sealed class PromotionDeduplicator
{
    public IReadOnlyList<Promotion> Deduplicate(IEnumerable<Promotion> promotions)
    {
        List<Promotion> result = [];

        foreach (Promotion promotion in promotions
                     .OrderBy(item => item.CanonicalUrl, StringComparer.Ordinal)
                     .ThenBy(item => item.Name, StringComparer.Ordinal))
        {
            Promotion? duplicate = result.FirstOrDefault(existing =>
                IsDefiniteDuplicate(existing, promotion));

            if (duplicate is null)
            {
                AddPossibleDuplicateWarning(result, promotion);
                result.Add(promotion);
            }
            else
            {
                Merge(duplicate, promotion);
            }
        }

        return result;
    }

    private static bool IsDefiniteDuplicate(Promotion left, Promotion right)
    {
        if (UrlNormalizer.Normalize(left.CanonicalUrl) == UrlNormalizer.Normalize(right.CanonicalUrl))
        {
            return true;
        }

        string leftFingerprint = PromotionIdentity.Fingerprint(left);
        string rightFingerprint = PromotionIdentity.Fingerprint(right);
        return leftFingerprint.Length > 2 && leftFingerprint == rightFingerprint;
    }

    private static void AddPossibleDuplicateWarning(IReadOnlyList<Promotion> existing, Promotion candidate)
    {
        bool possible = existing.Any(item =>
            TextNormalizer.NormalizeForComparison(item.Name) ==
            TextNormalizer.NormalizeForComparison(candidate.Name) &&
            TextNormalizer.NormalizeForComparison(item.Municipality) ==
            TextNormalizer.NormalizeForComparison(candidate.Municipality));

        if (possible)
        {
            candidate.Warnings = candidate.Warnings
                .Append("Posible duplicado: se conserva separado por falta de evidencia concluyente.")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    private static void Merge(Promotion target, Promotion source)
    {
        target.SourceUrls = target.SourceUrls.Concat(source.SourceUrls)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        target.BrochureUrls = target.BrochureUrls.Concat(source.BrochureUrls)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        target.Evidence = target.Evidence.Concat(source.Evidence)
            .OrderBy(item => item.Field, StringComparer.Ordinal)
            .ThenBy(item => item.SourceUrl, StringComparer.Ordinal)
            .ToArray();
        target.Tags = target.Tags.Concat(source.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        target.SourceConfidence = Math.Max(target.SourceConfidence, source.SourceConfidence);
        target.PriceFrom ??= source.PriceFrom;
        target.PriceTo ??= source.PriceTo;
        target.Latitude ??= source.Latitude;
        target.Longitude ??= source.Longitude;
        if (source.LocationPrecision > target.LocationPrecision)
        {
            target.LocationPrecision = source.LocationPrecision;
        }
    }
}
