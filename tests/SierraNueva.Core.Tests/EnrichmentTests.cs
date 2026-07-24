using SierraNueva.Contracts;
using SierraNueva.Core.Enrichment;
using SierraNueva.Core.Models;

namespace SierraNueva.Core.Tests;

public sealed class EnrichmentTests
{
    [Fact]
    public void Validation_OnlyKeepsMissingFieldsWithLiteralEvidence()
    {
        Promotion promotion = CreatePromotion();
        EnrichmentEvidenceDocument evidence = new()
        {
            PromotionId = promotion.Id,
            PromotionName = promotion.Name,
            Municipality = promotion.Municipality,
            CanonicalUrl = promotion.CanonicalUrl,
            Pages =
            [
                new()
                {
                    Url = promotion.CanonicalUrl,
                    Text = "Chalets de 3 dormitorios. Viviendas desde 475.000 euros."
                }
            ]
        };
        EnrichmentFieldProposal[] proposed =
        [
            new()
            {
                Field = "priceFrom",
                ValueText = "475000",
                SourceUrl = promotion.CanonicalUrl,
                EvidenceText = "Viviendas desde 475.000 euros",
                Confidence = 0.96m
            },
            new()
            {
                Field = "developerName",
                ValueText = "Promotora inventada",
                SourceUrl = promotion.CanonicalUrl,
                EvidenceText = "Promotora inventada",
                Confidence = 0.99m
            },
            new()
            {
                Field = "municipality",
                ValueText = "Moralzarzal",
                SourceUrl = promotion.CanonicalUrl,
                EvidenceText = "Moralzarzal",
                Confidence = 0.99m
            }
        ];

        IReadOnlyList<EnrichmentFieldProposal> valid = PromotionEnrichmentPolicy.Validate(
            evidence,
            PromotionEnrichmentPolicy.MissingFields(promotion),
            proposed,
            0.8m,
            out IReadOnlyList<string> warnings);

        EnrichmentFieldProposal field = Assert.Single(valid);
        Assert.Equal("priceFrom", field.Field);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void AcceptedProposal_OnlyFillsEmptyFieldAndAddsTraceableEvidence()
    {
        Promotion promotion = CreatePromotion();
        DateTimeOffset now = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        PromotionEnrichment enrichment = new()
        {
            Status = EnrichmentReviewStatus.Accepted,
            ReviewedAtUtc = now,
            EvidenceFetchedAtUtc = now.AddDays(-1),
            Fields =
            [
                new()
                {
                    Field = "priceFrom",
                    ValueText = "475000",
                    SourceUrl = promotion.CanonicalUrl,
                    EvidenceText = "Viviendas desde 475.000 euros",
                    Confidence = 0.96m
                }
            ]
        };

        PromotionEnrichmentPolicy.ApplyAccepted(promotion, enrichment, now);

        Assert.Equal(475_000m, promotion.PriceFrom);
        EvidenceItem evidence = Assert.Single(promotion.Evidence);
        Assert.Equal("reviewed-ai-enrichment", evidence.Extractor);
        Assert.Equal(enrichment.EvidenceFetchedAtUtc, evidence.CapturedAtUtc);

        promotion.PriceFrom = 490_000m;
        PromotionEnrichmentPolicy.ApplyAccepted(promotion, enrichment, now);
        Assert.Equal(490_000m, promotion.PriceFrom);
        Assert.Single(promotion.Evidence);
    }

    [Fact]
    public void AcceptedProposal_ExpiresAfterThirtyDays()
    {
        Promotion promotion = CreatePromotion();
        DateTimeOffset now = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        PromotionEnrichment enrichment = new()
        {
            Status = EnrichmentReviewStatus.Accepted,
            ReviewedAtUtc = now.AddDays(-31),
            EvidenceFetchedAtUtc = now.AddDays(-31),
            Fields =
            [
                new()
                {
                    Field = "priceFrom",
                    ValueText = "475000",
                    SourceUrl = promotion.CanonicalUrl,
                    EvidenceText = "Desde 475.000 euros",
                    Confidence = 0.96m
                }
            ]
        };

        PromotionEnrichmentPolicy.ApplyAccepted(promotion, enrichment, now);

        Assert.Null(promotion.PriceFrom);
        Assert.Empty(promotion.Evidence);
    }

    [Fact]
    public void AcceptedProposal_OnlyAppliesIndividuallyAcceptedFields()
    {
        Promotion promotion = CreatePromotion();
        DateTimeOffset now = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        PromotionEnrichment enrichment = new()
        {
            Status = EnrichmentReviewStatus.Accepted,
            ReviewedAtUtc = now,
            EvidenceFetchedAtUtc = now.AddDays(-1),
            Fields =
            [
                new()
                {
                    Field = "priceFrom",
                    ValueText = "475000",
                    SourceUrl = promotion.CanonicalUrl,
                    EvidenceText = "Viviendas desde 475.000 euros",
                    Confidence = 0.96m,
                    Status = EnrichmentReviewStatus.Accepted,
                    ReviewedAtUtc = now
                },
                new()
                {
                    Field = "builtAreaMinSqm",
                    ValueText = "160",
                    SourceUrl = promotion.CanonicalUrl,
                    EvidenceText = "160 metros cuadrados construidos",
                    Confidence = 0.91m,
                    Status = EnrichmentReviewStatus.Rejected,
                    ReviewedAtUtc = now
                }
            ]
        };

        PromotionEnrichmentPolicy.ApplyAccepted(promotion, enrichment, now);

        Assert.Equal(475_000m, promotion.PriceFrom);
        Assert.Null(promotion.BuiltAreaMinSqm);
        EvidenceItem evidence = Assert.Single(promotion.Evidence);
        Assert.Equal("priceFrom", evidence.Field);
    }

    private static Promotion CreatePromotion()
    {
        return new()
        {
            Id = "sn-cumbre",
            Name = "Residencial Cumbre",
            Municipality = "Moralzarzal",
            CanonicalUrl = "https://fixtures.sierranueva.test/cumbre",
            Active = true
        };
    }
}
