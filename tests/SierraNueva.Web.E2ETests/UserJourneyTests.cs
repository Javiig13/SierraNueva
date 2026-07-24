using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SierraNueva.Web.E2ETests;

[Collection(WebApplicationTestGroup.Name)]
public sealed class UserJourneyTests(WebApplicationFixture fixture)
{
    [Fact]
    public async Task FiltersMapAndDetailShareTheSamePromotion()
    {
        await using IBrowserContext context = await fixture.CreateContextAsync(1440, 1000);
        IPage page = await context.NewPageAsync();

        await page.GotoAsync(fixture.BaseAddress.AbsoluteUri);
        await Expect(page.Locator(".overview-bar h1"))
            .ToContainTextAsync("Casas nuevas en la Sierra");
        float overviewHeight = (await page.Locator(".overview-bar").BoundingBoxAsync())?.Height ?? 0;
        Assert.InRange(overviewHeight, 1, 170);
        await Expect(page.Locator(".promotion-card")).ToHaveCountAsync(4);
        await Expect(page.Locator(".price-marker")).ToHaveCountAsync(4);

        await page.GetByRole(AriaRole.Searchbox, new() { Name = "Buscar" })
            .FillAsync("Moralzarzal");

        await Expect(page.Locator(".promotion-card")).ToHaveCountAsync(1);
        await Expect(page.Locator(".promotion-card h3"))
            .ToHaveTextAsync("Residencial Cumbre");
        await Expect(page.Locator(".map"))
            .ToHaveAttributeAsync("data-feature-count", "1");
        await page.Locator(".promotion-card").HoverAsync();
        await Expect(page.Locator(".price-marker.is-active")).ToHaveCountAsync(1);
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(
            @"[?&]q=Moralzarzal(?:&|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        ILocator marker = page.Locator(".leaflet-interactive");
        await Expect(marker).ToHaveCountAsync(1);
        await marker.HoverAsync();
        await Expect(page.Locator(".promotion-card.is-highlighted")).ToHaveCountAsync(1);
        await marker.ClickAsync();
        await page.Locator(".map-popup")
            .GetByRole(AriaRole.Button, new() { Name = "Ver ficha" })
            .ClickAsync();

        ILocator dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.GetByRole(AriaRole.Heading, new()
        {
            Name = "Residencial Cumbre"
        })).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeHiddenAsync();
    }

    [Fact]
    public async Task MobileTabsRemainUsableWithoutHorizontalOverflow()
    {
        await using IBrowserContext context = await fixture.CreateContextAsync(390, 844);
        IPage page = await context.NewPageAsync();

        await page.GotoAsync(fixture.BaseAddress.AbsoluteUri);
        ILocator tabs = page.GetByRole(AriaRole.Tablist, new() { Name = "Vista" });
        await Expect(tabs).ToBeVisibleAsync();
        ILocator listTab = page.GetByRole(AriaRole.Tab, new() { Name = "Resultados" });
        ILocator mapTab = page.GetByRole(AriaRole.Tab, new() { Name = "Mapa" });
        await listTab.FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");

        await Expect(mapTab).ToBeFocusedAsync();
        await Expect(mapTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(page.Locator("#panel-map")).ToBeVisibleAsync();
        await Expect(page.Locator("#panel-list")).ToBeHiddenAsync();
        Assert.True(await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= document.documentElement.clientWidth"));
    }

    [Fact]
    public async Task KeyboardSemanticsAndKeyTextMeetBasicAccessibilityChecks()
    {
        await using IBrowserContext context = await fixture.CreateContextAsync(1440, 1000);
        IPage page = await context.NewPageAsync();

        await page.GotoAsync(fixture.BaseAddress.AbsoluteUri);
        await Expect(page.Locator(".overview-bar h1"))
            .ToContainTextAsync("Casas nuevas en la Sierra");
        await page.Keyboard.PressAsync("Tab");
        ILocator skipLink = page.GetByRole(AriaRole.Link, new() { Name = "Saltar al contenido" });
        await Expect(skipLink).ToBeFocusedAsync();
        await Expect(skipLink).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(
            "contenido",
            await page.EvaluateAsync<string>("() => document.activeElement?.id || ''"));

        string accessibilityTree = await page.Locator("body").AriaSnapshotAsync();
        Assert.Contains("Filtros de promociones", accessibilityTree, StringComparison.Ordinal);
        Assert.Contains("Mapa de promociones", accessibilityTree, StringComparison.Ordinal);
        Assert.Contains("Descargar datos", accessibilityTree, StringComparison.Ordinal);

        foreach (string selector in new[]
                 {
                     ".eyebrow",
                     ".download-link",
                     ".text-button",
                     ".source-link"
                 })
        {
            double ratio = await page.Locator(selector).First.EvaluateAsync<double>(
                """
                element => {
                  const parse = value => {
                    const parts = value.match(/[\d.]+/g).map(Number);
                    return parts.slice(0, 3).map(channel => {
                      const normalized = channel / 255;
                      return normalized <= 0.03928
                        ? normalized / 12.92
                        : Math.pow((normalized + 0.055) / 1.055, 2.4);
                    });
                  };
                  const luminance = rgb =>
                    0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
                  const foreground = luminance(parse(getComputedStyle(element).color));
                  let backgroundElement = element;
                  let backgroundColor;
                  do {
                    backgroundColor = getComputedStyle(backgroundElement).backgroundColor;
                    backgroundElement = backgroundElement.parentElement;
                  } while (backgroundElement && backgroundColor === "rgba(0, 0, 0, 0)");
                  const background = luminance(parse(backgroundColor || "rgb(255, 255, 255)"));
                  const light = Math.max(foreground, background);
                  const dark = Math.min(foreground, background);
                  return (light + 0.05) / (dark + 0.05);
                }
                """);
            Assert.True(ratio >= 4.5, $"{selector} tiene contraste {ratio:F2}:1.");
        }
    }
}
