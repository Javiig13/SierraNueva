using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Crawling;
using SierraNueva.Core.Models;
using SierraNueva.Core.Quality;
using SierraNueva.Infrastructure.Browser;
using SierraNueva.Infrastructure.Configuration;
using SierraNueva.Infrastructure.Crawling;
using SierraNueva.Infrastructure.Discovery;
using SierraNueva.Infrastructure.Documents;
using SierraNueva.Infrastructure.Extraction;
using SierraNueva.Infrastructure.Geocoding;
using SierraNueva.Infrastructure.Persistence;
using SierraNueva.Infrastructure.Security;
using SierraNueva.Infrastructure.Serialization;
using SierraNueva.Infrastructure.Time;

return await CrawlerApplication.RunAsync(args);

internal static class CrawlerApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintHelp();
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            return options.Command switch
            {
                "crawl" => await CrawlAsync(options, shutdown.Token),
                "validate-config" => await ValidateConfigAsync(options, shutdown.Token),
                "validate-data" => await ValidateDataAsync(options, shutdown.Token),
                _ => throw new ArgumentException($"Comando desconocido: {options.Command}")
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Ejecución cancelada de forma segura.");
            return 3;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"JSON no válido: {exception.Message}");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Fallo total: {exception.Message}");
            return 3;
        }
    }

    private static async Task<int> CrawlAsync(CliOptions options, CancellationToken cancellationToken)
    {
        ConfigurationBundle configuration = await LoadConfigurationAsync(options, cancellationToken);
        IReadOnlyList<string> errors = configuration.Loader.Validate(
            configuration.Settings,
            configuration.Sources,
            configuration.Municipalities);
        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Console.Error.WriteLine($"ERROR config: {error}");
            }

            return 2;
        }

        await using ServiceProvider services = BuildServices(
            options,
            configuration.Settings,
            configuration.Exclusions);
        CrawlPipeline pipeline = services.GetRequiredService<CrawlPipeline>();
        CrawlResult result = await pipeline.RunAsync(new()
        {
            Sources = configuration.Sources,
            Municipalities = configuration.Municipalities,
            Settings = configuration.Settings,
            OutputDirectory = options.Output,
            StateDirectory = options.State,
            SourceFilter = options.Source,
            MunicipalityFilter = options.Municipality,
            MaxPages = options.MaxPages,
            DisablePlaywright = options.NoPlaywright,
            DisableGeocoding = options.NoGeocoding,
            DryRun = options.DryRun
        }, cancellationToken);

        Console.WriteLine(
            $"Run {result.Run.RunId}: {result.Run.Status}. " +
            $"{result.Dataset.Promotions.Count} promociones; " +
            $"{result.Run.SuccessfulSources} fuentes correctas; " +
            $"{result.Run.FailedSources} fallidas.");
        if (options.DryRun)
        {
            Console.WriteLine("Dry-run: no se han escrito datos ni estado.");
        }

        return result.Run.Status switch
        {
            RunStatus.Success => 0,
            RunStatus.PartialSuccess => 1,
            _ => 3
        };
    }

    private static async Task<int> ValidateConfigAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        ConfigurationBundle configuration = await LoadConfigurationAsync(options, cancellationToken);
        IReadOnlyList<string> errors = configuration.Loader.Validate(
            configuration.Settings,
            configuration.Sources,
            configuration.Municipalities);
        if (errors.Count == 0)
        {
            Console.WriteLine(
                $"Configuración válida: {configuration.Sources.Count} fuentes y " +
                $"{configuration.Municipalities.Count} municipios.");
            return 0;
        }

        foreach (string error in errors)
        {
            Console.Error.WriteLine($"ERROR: {error}");
        }

        return 2;
    }

    private static async Task<int> ValidateDataAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        string promotionsPath = Path.Combine(options.Output, "promotions.json");
        string changesPath = Path.Combine(options.Output, "changes.json");
        string runPath = Path.Combine(options.Output, "run.json");
        string geoJsonPath = Path.Combine(options.Output, "promotions.geojson");
        string csvPath = Path.Combine(options.Output, "promotions.csv");
        string[] required = [promotionsPath, changesPath, runPath, geoJsonPath, csvPath];
        string[] missing = required.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
        {
            foreach (string path in missing)
            {
                Console.Error.WriteLine($"Falta {path}");
            }

            return 4;
        }

        ConfigurationBundle configuration = await LoadConfigurationAsync(options, cancellationToken);
        PromotionDataset? dataset;
        await using (FileStream stream = File.OpenRead(promotionsPath))
        {
            dataset = await JsonSerializer.DeserializeAsync<PromotionDataset>(
                stream,
                JsonDefaults.Compact,
                cancellationToken);
        }

        if (dataset is null || dataset.SchemaVersion != "1.0")
        {
            Console.Error.WriteLine("promotions.json no respeta el contrato 1.0.");
            return 4;
        }

        PromotionValidator validator = new();
        List<string> errors = [];
        foreach (Promotion promotion in dataset.Promotions)
        {
            ValidationResult result = validator.Validate(promotion, configuration.Municipalities);
            errors.AddRange(result.Errors.Select(error => $"{promotion.Id}: {error}"));
        }

        using JsonDocument geoJson = JsonDocument.Parse(
            await File.ReadAllTextAsync(geoJsonPath, cancellationToken));
        if (geoJson.RootElement.GetProperty("type").GetString() != "FeatureCollection")
        {
            errors.Add("promotions.geojson no es una FeatureCollection.");
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Console.Error.WriteLine($"ERROR data: {error}");
            }

            return 4;
        }

        Console.WriteLine($"Datos válidos: {dataset.Promotions.Count} promociones.");
        return 0;
    }

    private static async Task<ConfigurationBundle> LoadConfigurationAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        ConfigurationLoader loader = new();
        CrawlerSettings settings = await loader.LoadSettingsAsync(options.Config, cancellationToken);
        IReadOnlyList<SourceDefinition> sources = await loader.LoadSourcesAsync(
            options.Sources,
            cancellationToken);
        IReadOnlyList<MunicipalityDefinition> municipalities =
            await loader.LoadMunicipalitiesAsync(options.Municipalities, cancellationToken);
        DomainExclusions exclusions = await loader.LoadExclusionsAsync(
            options.Exclusions,
            cancellationToken);
        return new(loader, settings, sources, municipalities, exclusions);
    }

    private static ServiceProvider BuildServices(
        CliOptions options,
        CrawlerSettings settings,
        DomainExclusions exclusions)
    {
        ServiceCollection services = new();
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(console =>
            {
                console.SingleLine = true;
                console.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
        });
        services.AddHttpClient("crawler", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });
        services.AddHttpClient("nominatim")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 2,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        services.AddSingleton(exclusions);
        services.AddSingleton<IUrlPolicy, BlocklistUrlPolicy>();
        services.AddSingleton(new FileHttpMetadataCache(options.State));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<IDynamicPageRenderer, PlaywrightDynamicPageRenderer>();
        services.AddSingleton<IPromotionExtractor, LayeredPromotionExtractor>();
        services.AddSingleton<IPromotionStateRepository, JsonPromotionStateRepository>();
        services.AddSingleton<IPublicDataWriter, PublicDataWriter>();
        services.AddSingleton<InternalLinkDiscoveryProvider>();
        services.AddSingleton<IUrlDiscoveryProvider, ConfiguredUrlDiscoveryProvider>();
        services.AddSingleton<IUrlDiscoveryProvider, ManualFileDiscoveryProvider>();
        services.AddSingleton<IUrlDiscoveryProvider, SitemapDiscoveryProvider>();
        services.AddSingleton<IPageSource, RespectfulPageSource>();
        services.AddSingleton<MunicipalityCentroidGeocoder>();
        services.AddSingleton<IGeocoder>(provider =>
        {
            MunicipalityCentroidGeocoder fallback =
                provider.GetRequiredService<MunicipalityCentroidGeocoder>();
            return settings.Nominatim.Enabled
                ? new NominatimGeocoder(
                    provider.GetRequiredService<IHttpClientFactory>(),
                    settings.Nominatim,
                    settings.UserAgent,
                    options.State,
                    fallback)
                : fallback;
        });
        services.AddSingleton<CrawlPipeline>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            SierraNueva crawler

            Uso:
              dotnet run --project src/SierraNueva.Crawler -- crawl [opciones]
              dotnet run --project src/SierraNueva.Crawler -- validate-config [opciones]
              dotnet run --project src/SierraNueva.Crawler -- validate-data [opciones]

            Opciones:
              --config <ruta>          config/appsettings.json
              --sources <ruta>         config/sources.json
              --municipalities <ruta>  config/municipalities.json
              --exclusions <ruta>      config/domain-exclusions.json
              --output <ruta>          data/public
              --state <ruta>           data/state
              --municipality <nombre>  Filtrar municipio
              --source <id>            Filtrar fuente
              --max-pages <n>          Sobrescribir máximo
              --no-playwright          Deshabilitar fallback JavaScript
              --no-geocoding           Deshabilitar geocodificación
              --dry-run                Procesar sin escribir
              --verbose                Logging Debug
              --help                   Mostrar ayuda

            Códigos: 0 éxito, 1 parcial, 2 configuración, 3 fallo total, 4 datos inválidos.
            """);
    }

    private sealed record ConfigurationBundle(
        ConfigurationLoader Loader,
        CrawlerSettings Settings,
        IReadOnlyList<SourceDefinition> Sources,
        IReadOnlyList<MunicipalityDefinition> Municipalities,
        DomainExclusions Exclusions);
}

internal sealed class CliOptions
{
    private static readonly HashSet<string> BooleanOptions =
        new(StringComparer.Ordinal)
        {
            "--no-playwright",
            "--no-geocoding",
            "--dry-run",
            "--verbose",
            "--help",
            "-h"
        };

    public string Command { get; private init; } = "crawl";

    public string Config { get; private init; } = "config/appsettings.json";

    public string Sources { get; private init; } = "config/sources.json";

    public string Municipalities { get; private init; } = "config/municipalities.json";

    public string Exclusions { get; private init; } = "config/domain-exclusions.json";

    public string Output { get; private init; } = "data/public";

    public string State { get; private init; } = "data/state";

    public string? Municipality { get; private init; }

    public string? Source { get; private init; }

    public int? MaxPages { get; private init; }

    public bool NoPlaywright { get; private init; }

    public bool NoGeocoding { get; private init; }

    public bool DryRun { get; private init; }

    public bool Verbose { get; private init; }

    public bool ShowHelp { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        string command = args.FirstOrDefault(value => !value.StartsWith('-')) ?? "crawl";
        if (command is not ("crawl" or "validate-config" or "validate-data"))
        {
            throw new ArgumentException($"Comando desconocido: {command}");
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        HashSet<string> switches = new(StringComparer.Ordinal);
        int start = args.Length > 0 && args[0] == command ? 1 : 0;
        for (int index = start; index < args.Length; index++)
        {
            string argument = args[index];
            if (BooleanOptions.Contains(argument))
            {
                switches.Add(argument);
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException($"Opción incompleta o desconocida: {argument}");
            }

            values[argument] = args[++index];
        }

        int? maxPages = null;
        if (values.TryGetValue("--max-pages", out string? maxPagesText) &&
            (!int.TryParse(maxPagesText, out int parsed) || parsed < 1))
        {
            throw new ArgumentException("--max-pages debe ser un entero mayor que cero.");
        }
        else if (maxPagesText is not null)
        {
            maxPages = int.Parse(maxPagesText, System.Globalization.CultureInfo.InvariantCulture);
        }

        return new()
        {
            Command = command,
            Config = Get(values, "--config", "config/appsettings.json"),
            Sources = Get(values, "--sources", "config/sources.json"),
            Municipalities = Get(values, "--municipalities", "config/municipalities.json"),
            Exclusions = Get(values, "--exclusions", "config/domain-exclusions.json"),
            Output = Get(values, "--output", "data/public"),
            State = Get(values, "--state", "data/state"),
            Municipality = GetNullable(values, "--municipality"),
            Source = GetNullable(values, "--source"),
            MaxPages = maxPages,
            NoPlaywright = switches.Contains("--no-playwright"),
            NoGeocoding = switches.Contains("--no-geocoding"),
            DryRun = switches.Contains("--dry-run"),
            Verbose = switches.Contains("--verbose"),
            ShowHelp = switches.Contains("--help") || switches.Contains("-h")
        };
    }

    private static string Get(
        IReadOnlyDictionary<string, string> values,
        string key,
        string fallback)
    {
        return Path.GetFullPath(values.TryGetValue(key, out string? value) ? value : fallback);
    }

    private static string? GetNullable(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        return values.TryGetValue(key, out string? value) ? value : null;
    }
}
