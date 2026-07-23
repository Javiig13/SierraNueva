using Bunit;
using SierraNueva.Contracts;
using SierraNueva.Web.Components;
using SierraNueva.Web.Models;

namespace SierraNueva.Web.Tests;

public sealed class ComponentTests
{
    [Fact]
    public void PromotionCard_RendersNormalizedTextAndDirectLink()
    {
        using BunitContext context = new();
        Promotion promotion = new()
        {
            Id = "sn-test",
            Name = "Residencial Cumbre",
            Municipality = "Moralzarzal",
            CanonicalUrl = "https://example.com/cumbre",
            PropertyTypes = ["Pareado"],
            CommercialStatus = CommercialStatus.OnSale,
            SourceKind = SourceKind.OfficialPromoter,
            SourceConfidence = 0.9m,
            PriceFrom = 475_000m,
            BedroomsMin = 3,
            BuiltAreaMinSqm = 165,
            PlotAreaMinSqm = 280,
            LastSeenUtc = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero),
            Active = true
        };

        IRenderedComponent<PromotionCard> component = context.Render<PromotionCard>(
            parameters => parameters
                .Add(item => item.Promotion, promotion)
                .Add(item => item.IsNew, true));

        Assert.Contains("Residencial Cumbre", component.Markup, StringComparison.Ordinal);
        Assert.Contains("475", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Nueva", component.Markup, StringComparison.Ordinal);
        Assert.Equal(
            "https://example.com/cumbre",
            component.Find("a.source-link").GetAttribute("href"));
    }

    [Fact]
    public void FilterPanel_RendersVisibleLabelsAndMunicipalities()
    {
        using BunitContext context = new();

        IRenderedComponent<FilterPanel> component = context.Render<FilterPanel>(
            parameters => parameters
                .Add(item => item.Model, new PromotionFilter())
                .Add(item => item.Municipalities, ["El Boalo", "Moralzarzal"])
                .Add(item => item.Localities, ["Cerceda"]));

        Assert.Contains("Precio máximo", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Moralzarzal", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Solo promociones activas", component.Markup, StringComparison.Ordinal);
    }
}
