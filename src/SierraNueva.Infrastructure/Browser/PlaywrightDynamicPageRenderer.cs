using Microsoft.Playwright;
using SierraNueva.Core.Abstractions;

namespace SierraNueva.Infrastructure.Browser;

public sealed class PlaywrightDynamicPageRenderer : IDynamicPageRenderer
{
    public async Task<string?> RenderAsync(Uri url, CancellationToken cancellationToken)
    {
        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowser browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
        await using IBrowserContext context = await browser.NewContextAsync(new()
        {
            JavaScriptEnabled = true,
            ServiceWorkers = ServiceWorkerPolicy.Block
        });
        IPage page = await context.NewPageAsync();
        await page.RouteAsync("**/*", async route =>
        {
            if (route.Request.ResourceType is "image" or "media" or "font")
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => _ = page.CloseAsync());
        await page.GotoAsync(url.AbsoluteUri, new()
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 20_000
        });
        return await page.ContentAsync();
    }
}
