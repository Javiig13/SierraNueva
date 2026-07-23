using SierraNueva.Contracts;
using SierraNueva.Core.Changes;
using SierraNueva.Core.Identity;
using SierraNueva.Core.Quality;

namespace SierraNueva.Core.Tests;

public sealed class BusinessRulesTests
{
    [Fact]
    public void Deduplicator_MergesExactPromotionAndPreservesSources()
    {
        Promotion first = CreatePromotion("https://example.com/promo", 450_000m);
        first.SourceUrls = ["https://example.com/promo"];
        Promotion second = CreatePromotion("http://example.com/promo/", 450_000m);
        second.SourceUrls = ["https://microsite.example/promo"];

        IReadOnlyList<Promotion> result = new PromotionDeduplicator().Deduplicate([first, second]);

        Promotion merged = Assert.Single(result);
        Assert.Equal(2, merged.SourceUrls.Count);
        Assert.Equal(450_000m, merged.PriceFrom);
    }

    [Fact]
    public void Deduplicator_KeepsAmbiguousPromotionsSeparate()
    {
        Promotion first = CreatePromotion("https://a.example/norte", 450_000m);
        first.DeveloperName = "Promotora A";
        Promotion second = CreatePromotion("https://b.example/norte", 450_000m);
        second.DeveloperName = "Promotora B";

        IReadOnlyList<Promotion> result = new PromotionDeduplicator().Deduplicate([first, second]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Warnings.Count > 0);
    }

    [Fact]
    public void ChangeDetector_ReportsPriceChange()
    {
        Promotion previous = CreatePromotion("https://example.com/promo", 450_000m);
        previous.Id = "sn-1";
        Promotion current = CreatePromotion("https://example.com/promo", 475_000m);
        current.Id = "sn-1";

        PromotionChange? change = new ChangeDetector().Detect(
            previous,
            current,
            new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero));

        Assert.NotNull(change);
        FieldChange field = Assert.Single(change.Fields);
        Assert.Equal("priceFrom", field.Field);
        Assert.Equal("450000", field.PreviousValue);
        Assert.Equal("475000", field.CurrentValue);
    }

    [Fact]
    public void Validator_RejectsInvalidRangesAndMissingSource()
    {
        Promotion promotion = new()
        {
            Name = "Rango imposible",
            Municipality = "Moralzarzal",
            CanonicalUrl = string.Empty,
            PriceFrom = 500_000m,
            PriceTo = 400_000m
        };

        SierraNueva.Core.Models.ValidationResult result = new PromotionValidator().Validate(
            promotion,
            [new MunicipalityDefinition { OfficialName = "Moralzarzal" }]);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Confidence_OfficialSourceScoresAboveUnknownSource()
    {
        Promotion promotion = CreatePromotion("https://example.com/promo", 450_000m);
        SourceDefinition official = new()
        {
            SourceKind = SourceKind.OfficialPromoter,
            StartUrls = ["https://example.com/promo"]
        };
        SourceDefinition unknown = new() { SourceKind = SourceKind.Unknown };

        Assert.True(
            SourceConfidenceScorer.Score(official, promotion) >
            SourceConfidenceScorer.Score(unknown, promotion));
    }

    [Fact]
    public void Confidence_ExplainsEachAppliedSignal()
    {
        Promotion promotion = CreatePromotion("https://example.com/promo", 450_000m);
        promotion.DeveloperName = "Promotora Sierra";
        promotion.BrochureUrls = ["https://example.com/dossier.pdf"];
        promotion.Evidence =
        [
            new(), new(), new(), new()
        ];
        SourceDefinition source = new()
        {
            SourceKind = SourceKind.OfficialPromoter,
            StartUrls = ["https://example.com/inicio"]
        };

        SourceConfidenceExplanation explanation =
            SourceConfidenceScorer.Assess(source, promotion);

        Assert.Equal(0.82m, explanation.BaseScore);
        Assert.Equal(1m, explanation.FinalScore);
        Assert.Equal(
            [
                "source-kind",
                "brochure",
                "developer-identified",
                "evidence-rich",
                "configured-domain-match"
            ],
            explanation.Signals.Select(signal => signal.Code));
        Assert.Equal(explanation.FinalScore, explanation.Signals.Sum(signal => signal.Impact));
        Assert.Contains("dominio canónico", explanation.Summary, StringComparison.Ordinal);
    }

    private static Promotion CreatePromotion(string url, decimal price)
    {
        return new()
        {
            Name = "Residencial Norte",
            Municipality = "Moralzarzal",
            CanonicalUrl = url,
            PriceFrom = price,
            Active = true
        };
    }
}
