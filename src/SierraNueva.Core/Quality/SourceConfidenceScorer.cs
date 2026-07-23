using SierraNueva.Contracts;

namespace SierraNueva.Core.Quality;

public static class SourceConfidenceScorer
{
    public static decimal Score(SourceDefinition source, Promotion promotion)
    {
        decimal score = source.SourceKind switch
        {
            SourceKind.OfficialPromoter => 0.82m,
            SourceKind.OfficialMicrosite => 0.80m,
            SourceKind.PublicAuthority => 0.78m,
            SourceKind.CooperativeManager => 0.72m,
            SourceKind.ExclusiveMarketer => 0.68m,
            SourceKind.Builder => 0.68m,
            _ => 0.45m
        };

        if (promotion.BrochureUrls.Count > 0)
        {
            score += 0.05m;
        }

        if (!string.IsNullOrWhiteSpace(promotion.DeveloperName))
        {
            score += 0.04m;
        }

        if (promotion.Evidence.Count >= 4)
        {
            score += 0.04m;
        }

        if (source.StartUrls.Any(url =>
                string.Equals(
                    Uri.TryCreate(url, UriKind.Absolute, out Uri? configured) ? configured.Host : string.Empty,
                    Uri.TryCreate(promotion.CanonicalUrl, UriKind.Absolute, out Uri? canonical)
                        ? canonical.Host
                        : string.Empty,
                    StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.05m;
        }

        return Math.Clamp(decimal.Round(score, 2), 0m, 1m);
    }
}
