using SierraNueva.Contracts;
using SierraNueva.Web.Models;

namespace SierraNueva.Web.Tests;

public sealed class FreshnessTests
{
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 9, 27, 5, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, false)]
    [InlineData(35, false)]
    [InlineData(36, false)]
    [InlineData(37, true)]
    [InlineData(720, true)]
    public void Freshness_UsesTheReadingTimeRatherThanThePublishedFlag(int elapsedHours, bool stale)
    {
        SiteData data = new()
        {
            Promotions = new() { GeneratedAtUtc = CheckedAt },
            Run = new()
            {
                DataFreshness = new()
                {
                    IsStale = false,
                    NewestSourceCheckUtc = CheckedAt,
                    StaleAfterHours = 36
                }
            }
        };

        Assert.Equal(stale, data.IsStale(CheckedAt.AddHours(elapsedHours)));
    }

    [Fact]
    public void Freshness_DoesNotHideAnOldSourceCheckBehindANewPublication()
    {
        DateTimeOffset now = CheckedAt.AddDays(7);
        SiteData data = new()
        {
            Promotions = new() { GeneratedAtUtc = now },
            Run = new()
            {
                DataFreshness = new()
                {
                    NewestSourceCheckUtc = CheckedAt,
                    StaleAfterHours = 36
                }
            }
        };

        Assert.True(data.IsStale(now));
    }

    [Fact]
    public void Freshness_PreservesAnExplicitStaleFlag()
    {
        SiteData data = new()
        {
            Promotions = new() { GeneratedAtUtc = CheckedAt },
            Run = new() { DataFreshness = new() { IsStale = true } }
        };

        Assert.True(data.IsStale(CheckedAt));
    }

    [Fact]
    public void Freshness_UnknownCheckIsNotPresentedAsFresh()
    {
        Assert.True(UiFormatting.IsStale(default, 36, CheckedAt));
    }

    [Fact]
    public void Freshness_RejectsAnInvalidThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UiFormatting.IsStale(CheckedAt, 0, CheckedAt));
    }
}
