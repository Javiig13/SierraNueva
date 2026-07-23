using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;
using SierraNueva.Core.Crawling;
using SierraNueva.Core.Discovery;
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
                "discover-opportunities" => await DiscoverOpportunitiesAsync(
                    options,
                    shutdown.Token),
                "review-opportunity" => await ReviewOpportunityAsync(
                    options,
                    shutdown.Token),
                "coverage-status" => await CoverageStatusAsync(
                    options,
                    shutdown.Token),
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
            configuration.Municipalities,
            configuration.CentroidCatalog);
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
            configuration.Municipalities,
            configuration.CentroidCatalog);
        OpportunityDiscoveryCatalog opportunityCatalog =
            await configuration.Loader.LoadOpportunityCatalogAsync(
                options.DiscoverySources,
                cancellationToken);
        errors = errors.Concat(
                configuration.Loader.ValidateOpportunityCatalog(
                    opportunityCatalog,
                    configuration.Municipalities))
            .ToArray();
        if (errors.Count == 0)
        {
            Console.WriteLine(
                $"Configuración válida: {configuration.Sources.Count} fuentes y " +
                $"{configuration.Municipalities.Count} municipios; " +
                $"{configuration.CentroidCatalog.Sources.Count} centroides trazables; " +
                $"{opportunityCatalog.Sources.Count} fuentes de radar.");
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

    private static async Task<int> DiscoverOpportunitiesAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        ConfigurationLoader loader = new();
        OpportunityDiscoveryCatalog catalog = await loader.LoadOpportunityCatalogAsync(
            options.DiscoverySources,
            cancellationToken);
        IReadOnlyList<MunicipalityDefinition> municipalities =
            await loader.LoadMunicipalitiesAsync(options.Municipalities, cancellationToken);
        IReadOnlyList<SourceDefinition> knownSources = await loader.LoadSourcesAsync(
            options.Sources,
            cancellationToken);
        IReadOnlyList<string> errors = loader.ValidateOpportunityCatalog(
            catalog,
            municipalities);
        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Console.Error.WriteLine($"ERROR radar: {error}");
            }

            return 2;
        }

        CrawlerSettings settings = await loader.LoadSettingsAsync(
            options.Config,
            cancellationToken);
        DateOnly to = options.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly from = options.From ??
                        to.AddDays(-Math.Max(0, catalog.DefaultLookbackDays - 1));

        await using ServiceProvider services = BuildOpportunityServices(settings);
        OpportunityDiscoveryPipeline pipeline =
            services.GetRequiredService<OpportunityDiscoveryPipeline>();
        OpportunityDiscoveryResult result = await pipeline.RunAsync(
            new()
            {
                Catalog = catalog,
                Municipalities = municipalities,
                StateDirectory = options.State,
                From = from,
                To = to,
                SourceFilter = options.Source,
                KnownPromotionUrls = knownSources
                    .Where(source => source.Enabled)
                    .SelectMany(source => source.StartUrls)
                    .ToArray(),
                DryRun = options.DryRun
            },
            cancellationToken);

        int failed = result.Run.Sources.Count(source => !source.Success);
        Console.WriteLine(
            $"Radar {result.Run.RunId}: {result.Run.NewCandidates} candidatos nuevos, " +
            $"{result.Run.UpdatedCandidates} actualizados, " +
            $"{result.State.Candidates.Count} acumulados; {failed} fuentes fallidas.");
        Console.WriteLine(
            $"Cobertura: {result.State.Coverage.MunicipalitiesWithHealthyCoverage}/" +
            $"{result.State.Coverage.MunicipalitiesTotal} municipios con vigilancia sana; " +
            $"{result.State.Coverage.MunicipalitiesWithHealthyDirectSource} con canal " +
            $"municipal directo; {result.State.Coverage.HealthySources} fuentes sanas, " +
            $"{result.State.Coverage.DegradedSources} degradadas y " +
            $"{result.State.Coverage.FailingSources} en fallo reiterado.");
        foreach (OpportunitySourceRun source in result.Run.Sources)
        {
            Console.WriteLine(
                $"  {source.SourceId}: {(source.Success ? "ok" : "fallo")}, " +
                $"{source.ItemsRead} entradas, {source.CandidatesMatched} candidatos.");
        }

        foreach (OpportunitySourceRun source in result.Run.Sources.Where(source => !source.Success))
        {
            Console.Error.WriteLine($"ERROR radar {source.SourceId}: {source.Error}");
        }

        if (options.DryRun)
        {
            Console.WriteLine("Dry-run: no se ha escrito estado privado.");
        }

        return failed == 0 ? 0 : result.Run.Sources.Any(source => source.Success) ? 1 : 3;
    }

    private static async Task<int> ReviewOpportunityAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Candidate) ||
            !options.OpportunityStatus.HasValue)
        {
            Console.Error.WriteLine(
                "review-opportunity requiere --candidate <id> y --status <estado>.");
            return 2;
        }

        JsonOpportunityStateRepository repository = new();
        OpportunityRadarState state = await repository.LoadAsync(
            options.State,
            cancellationToken);
        OpportunityCandidate? target = state.Candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, options.Candidate, StringComparison.Ordinal));
        if (target is null)
        {
            Console.Error.WriteLine($"No existe el candidato '{options.Candidate}'.");
            return 2;
        }

        OpportunityCandidate reviewed = CopyWithStatus(
            target,
            options.OpportunityStatus.Value);
        OpportunityRadarState updated = new()
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            LastRun = state.LastRun,
            Candidates = state.Candidates
                .Select(candidate => candidate.Id == reviewed.Id ? reviewed : candidate)
                .ToArray(),
            SourceHealth = state.SourceHealth,
            Coverage = state.Coverage
        };
        await repository.SaveAsync(options.State, updated, cancellationToken);
        Console.WriteLine(
            $"Candidato {reviewed.Id}: estado {reviewed.Status} guardado en " +
            $"'{options.State}'.");
        return 0;
    }

    private static async Task<int> CoverageStatusAsync(
        CliOptions options,
        CancellationToken cancellationToken)
    {
        OpportunityRadarState state = await new JsonOpportunityStateRepository().LoadAsync(
            options.State,
            cancellationToken);
        OpportunityCoverageSnapshot coverage = state.Coverage;
        if (coverage.GeneratedAtUtc == default)
        {
            Console.Error.WriteLine(
                "Todavía no existe una instantánea de cobertura. " +
                "Ejecuta discover-opportunities sin --dry-run.");
            return 1;
        }

        Console.WriteLine(
            $"Cobertura {coverage.GeneratedAtUtc:O}: " +
            $"{coverage.MunicipalitiesWithHealthyCoverage}/{coverage.MunicipalitiesTotal} " +
            $"municipios con vigilancia sana; " +
            $"{coverage.MunicipalitiesWithHealthyDirectSource} con canal directo.");
        Console.WriteLine(
            $"Fuentes: {coverage.HealthySources} sanas, " +
            $"{coverage.DegradedSources} degradadas, " +
            $"{coverage.FailingSources} en fallo reiterado; " +
            $"{coverage.PendingCandidates} candidatos pendientes.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        OpportunitySourceHealth[] overdueSources = state.SourceHealth
            .Where(source =>
                source.NextCheckDueUtc.HasValue &&
                source.NextCheckDueUtc.Value < now)
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .ToArray();
        foreach (OpportunitySourceHealth source in overdueSources)
        {
            Console.WriteLine(
                $"  ATRASADA {source.SourceId}: revisión prevista antes de " +
                $"{source.NextCheckDueUtc:O}.");
        }

        foreach (OpportunitySourceHealth source in state.SourceHealth.Where(source =>
                     source.Status is
                         OpportunitySourceHealthStatus.Degraded or
                         OpportunitySourceHealthStatus.Failing))
        {
            Console.WriteLine(
                $"  FUENTE {source.SourceId}: {source.Status}; " +
                $"{string.Join(" | ", source.Issues)}");
        }

        foreach (MunicipalityOpportunityCoverage municipality in
                 coverage.Municipalities.Where(municipality =>
                     municipality.Status is
                         MunicipalityCoverageStatus.CentralOnly or
                         MunicipalityCoverageStatus.Degraded or
                         MunicipalityCoverageStatus.NotChecked))
        {
            Console.WriteLine(
                $"  MUNICIPIO {municipality.Municipality}: {municipality.Status}; " +
                $"{municipality.HealthyDirectSources}/" +
                $"{municipality.ConfiguredDirectSources} canales directos sanos.");
        }

        return coverage.DegradedSources > 0 ||
               coverage.FailingSources > 0 ||
               overdueSources.Length > 0 ||
               coverage.MunicipalitiesWithHealthyCoverage < coverage.MunicipalitiesTotal
            ? 1
            : 0;
    }

    private static OpportunityCandidate CopyWithStatus(
        OpportunityCandidate candidate,
        OpportunityCandidateStatus status)
    {
        return new()
        {
            Id = candidate.Id,
            SourceId = candidate.SourceId,
            SourceName = candidate.SourceName,
            SourceKind = candidate.SourceKind,
            ExternalId = candidate.ExternalId,
            Title = candidate.Title,
            Summary = candidate.Summary,
            OfficialUrl = candidate.OfficialUrl,
            PublishedAtUtc = candidate.PublishedAtUtc,
            Municipality = candidate.Municipality,
            Kind = candidate.Kind,
            Confidence = candidate.Confidence,
            MatchedTerms = candidate.MatchedTerms,
            FirstSeenUtc = candidate.FirstSeenUtc,
            LastSeenUtc = candidate.LastSeenUtc,
            Status = status
        };
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
        MunicipalityCentroidCatalog centroidCatalog =
            await loader.LoadCentroidSourcesAsync(options.CentroidSources, cancellationToken);
        DomainExclusions exclusions = await loader.LoadExclusionsAsync(
            options.Exclusions,
            cancellationToken);
        return new(loader, settings, sources, municipalities, centroidCatalog, exclusions);
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
            .ConfigurePrimaryHttpMessageHandler(provider =>
                provider.GetRequiredService<DnsRebindingSafeHandlerFactory>().Create(5));
        services.AddHttpClient("nominatim")
            .ConfigurePrimaryHttpMessageHandler(provider =>
                provider.GetRequiredService<DnsRebindingSafeHandlerFactory>().Create(2));

        services.AddSingleton(exclusions);
        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<DnsRebindingSafeHandlerFactory>();
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

    private static ServiceProvider BuildOpportunityServices(CrawlerSettings settings)
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddSimpleConsole());
        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<DnsRebindingSafeHandlerFactory>();
        services.AddHttpClient(
                "opportunity-discovery",
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
                })
            .ConfigurePrimaryHttpMessageHandler(provider =>
                provider.GetRequiredService<DnsRebindingSafeHandlerFactory>().Create(
                    5,
                    useSessionCookies: true));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<OpportunityFeedParser>();
        services.AddSingleton<IOpportunityFeedReader, OpportunityFeedReader>();
        services.AddSingleton<IOpportunityStateRepository, JsonOpportunityStateRepository>();
        services.AddSingleton<OpportunityDiscoveryPipeline>();
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
              dotnet run --project src/SierraNueva.Crawler -- discover-opportunities [opciones]
              dotnet run --project src/SierraNueva.Crawler -- review-opportunity [opciones]
              dotnet run --project src/SierraNueva.Crawler -- coverage-status [opciones]

            Opciones:
              --config <ruta>          config/appsettings.json
              --sources <ruta>         config/sources.json
              --municipalities <ruta>  config/municipalities.json
              --centroid-sources <ruta> config/municipality-centroids.json
              --exclusions <ruta>      config/domain-exclusions.json
              --discovery-sources <ruta> config/discovery-sources.json
              --output <ruta>          data/public
              --state <ruta>           data/state
              --municipality <nombre>  Filtrar municipio
              --source <id>            Filtrar fuente
              --max-pages <n>          Sobrescribir máximo
              --from <aaaa-mm-dd>      Inicio de ventana del radar
              --to <aaaa-mm-dd>        Fin de ventana del radar
              --candidate <id>         Candidato privado que revisar
              --status <estado>        new, monitoring, rejected, verifiedSource o stale
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
        MunicipalityCentroidCatalog CentroidCatalog,
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

    public string CentroidSources { get; private init; } =
        "config/municipality-centroids.json";

    public string Exclusions { get; private init; } = "config/domain-exclusions.json";

    public string DiscoverySources { get; private init; } =
        "config/discovery-sources.json";

    public string Output { get; private init; } = "data/public";

    public string State { get; private init; } = "data/state";

    public string? Municipality { get; private init; }

    public string? Source { get; private init; }

    public int? MaxPages { get; private init; }

    public DateOnly? From { get; private init; }

    public DateOnly? To { get; private init; }

    public string? Candidate { get; private init; }

    public OpportunityCandidateStatus? OpportunityStatus { get; private init; }

    public bool NoPlaywright { get; private init; }

    public bool NoGeocoding { get; private init; }

    public bool DryRun { get; private init; }

    public bool Verbose { get; private init; }

    public bool ShowHelp { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        string command = args.FirstOrDefault(value => !value.StartsWith('-')) ?? "crawl";
        if (command is not (
            "crawl" or
            "validate-config" or
            "validate-data" or
            "discover-opportunities" or
            "review-opportunity" or
            "coverage-status"))
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

        DateOnly? from = ParseDate(values, "--from");
        DateOnly? to = ParseDate(values, "--to");
        OpportunityCandidateStatus? opportunityStatus = null;
        if (values.TryGetValue("--status", out string? statusText) &&
            !Enum.TryParse(statusText, ignoreCase: true, out OpportunityCandidateStatus parsedStatus))
        {
            throw new ArgumentException("--status no es un estado de candidato válido.");
        }
        else if (statusText is not null)
        {
            opportunityStatus = Enum.Parse<OpportunityCandidateStatus>(
                statusText,
                ignoreCase: true);
        }

        return new()
        {
            Command = command,
            Config = Get(values, "--config", "config/appsettings.json"),
            Sources = Get(values, "--sources", "config/sources.json"),
            Municipalities = Get(values, "--municipalities", "config/municipalities.json"),
            CentroidSources = Get(
                values,
                "--centroid-sources",
                "config/municipality-centroids.json"),
            Exclusions = Get(values, "--exclusions", "config/domain-exclusions.json"),
            DiscoverySources = Get(
                values,
                "--discovery-sources",
                "config/discovery-sources.json"),
            Output = Get(values, "--output", "data/public"),
            State = Get(values, "--state", "data/state"),
            Municipality = GetNullable(values, "--municipality"),
            Source = GetNullable(values, "--source"),
            MaxPages = maxPages,
            From = from,
            To = to,
            Candidate = GetNullable(values, "--candidate"),
            OpportunityStatus = opportunityStatus,
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

    private static DateOnly? ParseDate(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        if (!values.TryGetValue(key, out string? value))
        {
            return null;
        }

        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out DateOnly parsed)
            ? parsed
            : throw new ArgumentException($"{key} debe usar el formato aaaa-mm-dd.");
    }
}
