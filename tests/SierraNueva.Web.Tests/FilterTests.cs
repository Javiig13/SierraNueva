using SierraNueva.Contracts;
using SierraNueva.Web.Models;

namespace SierraNueva.Web.Tests;

public sealed class FilterTests
{
    [Fact]
    public void Filter_AppliesCombinedCriteriaToOneDataset()
    {
        PromotionFilter filter = new()
        {
            Search = "cumbre",
            PriceMax = 500_000m,
            BedroomsMin = 3,
            ActiveOnly = true
        };
        filter.Municipalities.Add("Moralzarzal");
        Promotion match = CreatePromotion(
            "sn-1",
            "Residencial Cumbre",
            "Moralzarzal",
            475_000m,
            4);
        Promotion expensive = CreatePromotion(
            "sn-2",
            "Cumbre Alta",
            "Moralzarzal",
            800_000m,
            4);
        Promotion otherTown = CreatePromotion(
            "sn-3",
            "Residencial Cumbre",
            "Soto del Real",
            450_000m,
            4);

        Promotion result = Assert.Single(filter.Apply(
            [match, expensive, otherTown],
            new HashSet<string>(),
            new HashSet<string>()));

        Assert.Equal(match.Id, result.Id);
    }

    [Fact]
    public void QueryCodec_RoundTripsShareableFilters()
    {
        PromotionFilter source = new()
        {
            Search = "obra nueva",
            PriceMax = 650_000m,
            PropertyType = "Pareado",
            ExactLocationOnly = true,
            Sort = "price-asc"
        };
        source.Municipalities.Add("El Boalo");
        source.Municipalities.Add("Moralzarzal");

        string query = FilterQueryCodec.Build(source);
        PromotionFilter restored = new();
        FilterQueryCodec.Apply(new Uri($"https://localhost/?{query}"), restored);

        Assert.Equal(source.Search, restored.Search);
        Assert.Equal(source.PriceMax, restored.PriceMax);
        Assert.Equal(source.PropertyType, restored.PropertyType);
        Assert.True(restored.ExactLocationOnly);
        Assert.Equal(source.Sort, restored.Sort);
        Assert.True(source.Municipalities.SetEquals(restored.Municipalities));
    }

    private static Promotion CreatePromotion(
        string id,
        string name,
        string municipality,
        decimal price,
        int bedrooms)
    {
        return new()
        {
            Id = id,
            Name = name,
            Municipality = municipality,
            CanonicalUrl = $"https://example.com/{id}",
            PriceFrom = price,
            BedroomsMin = bedrooms,
            BedroomsMax = bedrooms,
            Active = true,
            LastChangedUtc = DateTimeOffset.UtcNow
        };
    }
}
