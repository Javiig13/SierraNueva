using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;

namespace SierraNueva.Web.E2ETests;

public sealed class WebApplicationFixture : IAsyncLifetime
{
    private const string LeafletStub =
        """
        (() => {
          let currentElement = null;
          window.L = {
            map(element) {
              currentElement = element;
              element.dataset.leafletInitialized = "true";
              return {
                setView() { return this; },
                removeLayer(layer) { layer.remove(); },
                fitBounds() {},
                invalidateSize() {},
                remove() { element.replaceChildren(); }
              };
            },
            tileLayer() {
              return { addTo() { return this; } };
            },
            circleMarker() {
              return {};
            },
            geoJSON(collection, options) {
              const markers = [];
              for (const feature of collection.features || []) {
                options.pointToLayer(feature, {});
                const marker = document.createElement("button");
                marker.type = "button";
                marker.className = "leaflet-marker-icon e2e-map-marker";
                marker.setAttribute("aria-label", `Abrir ${feature.properties.name}`);
                const layer = {
                  bindPopup(content) {
                    marker.addEventListener("click", () => {
                      currentElement.querySelector(".map-popup")?.remove();
                      currentElement.appendChild(content);
                    });
                  }
                };
                options.onEachFeature(feature, layer);
                currentElement.appendChild(marker);
                markers.push(marker);
              }
              currentElement.dataset.featureCount = String(markers.length);
              return {
                addTo() { return this; },
                getBounds() {
                  return {
                    isValid() { return markers.length > 0; },
                    pad() { return this; }
                  };
                },
                remove() {
                  for (const marker of markers) marker.remove();
                  currentElement.querySelector(".map-popup")?.remove();
                }
              };
            }
          };
        })();
        """;

    private WebApplication? _application;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceWebRoot = Path.Combine(
            repositoryRoot,
            "src",
            "SierraNueva.Web",
            "wwwroot");
        string buildWebRoot = Path.Combine(
            repositoryRoot,
            "src",
            "SierraNueva.Web",
            "bin",
            "Release",
            "net10.0",
            "wwwroot");
        if (!File.Exists(Path.Combine(sourceWebRoot, "index.html")) ||
            !Directory.Exists(Path.Combine(buildWebRoot, "_framework")))
        {
            throw new DirectoryNotFoundException(
                "No están disponibles los recursos fuente y compilados necesarios para E2E.");
        }

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _application = builder.Build();
        CompositeFileProvider files = new(
            new PhysicalFileProvider(sourceWebRoot),
            new PhysicalFileProvider(buildWebRoot));
        FileExtensionContentTypeProvider contentTypes = new();
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".geojson"] = "application/geo+json";
        contentTypes.Mappings[".pdb"] = "application/octet-stream";
        _application.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        _application.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = contentTypes
        });
        _application.MapFallback(async context =>
            await context.Response.SendFileAsync(Path.Combine(sourceWebRoot, "index.html")));
        await _application.StartAsync();

        IServer server = _application.Services.GetRequiredService<IServer>();
        string address = server.Features.Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
        BaseAddress = new(address);

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            ExecutablePath = FindBrowserExecutable()
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }

    public async Task<IBrowserContext> CreateContextAsync(int width, int height)
    {
        IBrowser browser = _browser ??
            throw new InvalidOperationException("Chromium no está inicializado.");
        IBrowserContext context = await browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = width, Height = height },
            Locale = "es-ES",
            ColorScheme = ColorScheme.Light
        });
        context.Page += (_, page) =>
        {
            page.PageError += (_, error) =>
                Console.Error.WriteLine($"E2E page error: {error}");
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    Console.Error.WriteLine($"E2E console error: {message.Text}");
                }
            };
        };
        await context.AddInitScriptAsync(LeafletStub);
        await context.RouteAsync("**/*", async route =>
        {
            Uri uri = new(route.Request.Url);
            if (uri.Host is "127.0.0.1" or "localhost")
            {
                await route.ContinueAsync();
            }
            else
            {
                await route.AbortAsync();
            }
        });
        return context;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "SierraNueva.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
               throw new DirectoryNotFoundException("No se encontró la raíz de SierraNueva.");
    }

    private static string? FindBrowserExecutable()
    {
        string? configured = Environment.GetEnvironmentVariable("SIERRANUEVA_BROWSER_PATH");
        string[] candidates =
        [
            configured ?? string.Empty,
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/usr/bin/google-chrome"
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}

[CollectionDefinition(Name)]
public sealed class WebApplicationTestGroup : ICollectionFixture<WebApplicationFixture>
{
    public const string Name = "SierraNueva web E2E";
}
