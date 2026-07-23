using SierraNueva.Contracts;

namespace SierraNueva.Core.Quality;

public static class SourceConfidenceScorer
{
    public static decimal Score(SourceDefinition source, Promotion promotion)
    {
        return Assess(source, promotion).FinalScore;
    }

    public static SourceConfidenceExplanation Assess(
        SourceDefinition source,
        Promotion promotion)
    {
        (decimal baseScore, string sourceLabel) = source.SourceKind switch
        {
            SourceKind.OfficialPromoter => (0.82m, "Promotora oficial configurada."),
            SourceKind.OfficialMicrosite => (0.80m, "Micrositio oficial configurado."),
            SourceKind.PublicAuthority => (0.78m, "Autoridad pública configurada."),
            SourceKind.CooperativeManager => (0.72m, "Gestora de cooperativa configurada."),
            SourceKind.ExclusiveMarketer => (0.68m, "Comercializadora exclusiva configurada."),
            SourceKind.Builder => (0.68m, "Constructora configurada."),
            _ => (0.45m, "Tipo de fuente no verificado.")
        };
        List<SourceConfidenceSignal> signals =
        [
            CreateSignal("source-kind", sourceLabel, baseScore)
        ];

        if (promotion.BrochureUrls.Count > 0)
        {
            signals.Add(CreateSignal(
                "brochure",
                "Existe un dossier comercial enlazado.",
                0.05m));
        }

        if (!string.IsNullOrWhiteSpace(promotion.DeveloperName))
        {
            signals.Add(CreateSignal(
                "developer-identified",
                "La promotora está identificada en el contenido.",
                0.04m));
        }

        if (promotion.Evidence.Count >= 4)
        {
            signals.Add(CreateSignal(
                "evidence-rich",
                "Hay cuatro o más evidencias estructuradas.",
                0.04m));
        }

        if (HasConfiguredDomainMatch(source, promotion))
        {
            signals.Add(CreateSignal(
                "configured-domain-match",
                "El dominio canónico coincide con una URL inicial configurada.",
                0.05m));
        }

        decimal finalScore = Math.Clamp(
            decimal.Round(signals.Sum(signal => signal.Impact), 2),
            0m,
            1m);
        return new()
        {
            BaseScore = baseScore,
            FinalScore = finalScore,
            Summary = string.Join(' ', signals.Select(signal => signal.Label)),
            Signals = signals
        };
    }

    private static bool HasConfiguredDomainMatch(
        SourceDefinition source,
        Promotion promotion)
    {
        string canonicalHost = Uri.TryCreate(
            promotion.CanonicalUrl,
            UriKind.Absolute,
            out Uri? canonical)
            ? canonical.Host
            : string.Empty;
        return canonicalHost.Length > 0 &&
               source.StartUrls.Any(url =>
                   string.Equals(
                       Uri.TryCreate(url, UriKind.Absolute, out Uri? configured)
                           ? configured.Host
                           : string.Empty,
                       canonicalHost,
                       StringComparison.OrdinalIgnoreCase));
    }

    private static SourceConfidenceSignal CreateSignal(
        string code,
        string label,
        decimal impact)
    {
        return new()
        {
            Code = code,
            Label = label,
            Impact = impact
        };
    }
}
