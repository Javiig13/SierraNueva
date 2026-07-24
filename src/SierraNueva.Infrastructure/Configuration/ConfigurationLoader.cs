using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Core.Models;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Configuration;

public sealed class ConfigurationLoader
{
    public async Task<CrawlerSettings> LoadSettingsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        SettingsEnvelope? envelope = await JsonSerializer.DeserializeAsync<SettingsEnvelope>(
            stream,
            JsonDefaults.Compact,
            cancellationToken);
        return envelope?.Crawler ?? throw new InvalidDataException(
            $"El archivo '{path}' no contiene la sección 'crawler'.");
    }

    public Task<IReadOnlyList<SourceDefinition>> LoadSourcesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        return LoadListAsync<SourceDefinition>(path, "sources", cancellationToken);
    }

    public Task<IReadOnlyList<MunicipalityDefinition>> LoadMunicipalitiesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        return LoadListAsync<MunicipalityDefinition>(path, "municipalities", cancellationToken);
    }

    public async Task<MunicipalityCentroidCatalog> LoadCentroidSourcesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MunicipalityCentroidCatalog>(
                   stream,
                   JsonDefaults.Compact,
                   cancellationToken)
               ?? throw new InvalidDataException(
                   $"El archivo '{path}' no contiene un catálogo de centroides.");
    }

    public async Task<DomainExclusions> LoadExclusionsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DomainExclusions>(
                   stream,
                   JsonDefaults.Compact,
                   cancellationToken)
               ?? new();
    }

    public async Task<OpportunityDiscoveryCatalog> LoadOpportunityCatalogAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        OpportunityDiscoveryCatalog? catalog =
            await JsonSerializer.DeserializeAsync<OpportunityDiscoveryCatalog>(
                stream,
                JsonDefaults.Compact,
                cancellationToken);
        return catalog ?? throw new InvalidDataException(
            $"El archivo '{path}' no contiene un catálogo de radar.");
    }

    public IReadOnlyList<string> ValidateOpportunityCatalog(
        OpportunityDiscoveryCatalog catalog,
        IReadOnlyList<MunicipalityDefinition>? municipalities = null)
    {
        List<string> errors = [];
        if (catalog.SchemaVersion != "1.0")
        {
            errors.Add("El catálogo del radar debe usar el contrato 1.0.");
        }

        if (catalog.DefaultLookbackDays is < 0 or > 366)
        {
            errors.Add("defaultLookbackDays debe estar entre 0 y 366.");
        }

        if (catalog.Terms.Count == 0 ||
            catalog.Terms.Any(rule => string.IsNullOrWhiteSpace(rule.Term)))
        {
            errors.Add("El radar necesita términos de oportunidad no vacíos.");
        }

        if (catalog.ContextTerms.Count == 0 ||
            catalog.ContextTerms.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("El radar necesita términos de contexto inmobiliario.");
        }

        foreach (IGrouping<string, OpportunitySourceDefinition> duplicate in catalog.Sources
                     .GroupBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Id de fuente de radar duplicado: {duplicate.Key}.");
        }

        foreach (OpportunitySourceDefinition source in catalog.Sources.Where(item => item.Enabled))
        {
            if (string.IsNullOrWhiteSpace(source.Id) ||
                string.IsNullOrWhiteSpace(source.Name))
            {
                errors.Add("Toda fuente de radar habilitada necesita id y nombre.");
            }

            if (string.IsNullOrWhiteSpace(source.FixturePath) &&
                string.IsNullOrWhiteSpace(source.UrlTemplate))
            {
                errors.Add($"La fuente de radar '{source.Id}' no tiene URL ni fixture.");
            }

            if (source.MaxItems is < 1 or > 20_000)
            {
                errors.Add($"maxItems de '{source.Id}' debe estar entre 1 y 20000.");
            }

            if (!string.IsNullOrWhiteSpace(source.FixturePath) &&
                !File.Exists(source.FixturePath))
            {
                errors.Add($"No existe la fixture de radar '{source.FixturePath}'.");
            }

            if (!string.IsNullOrWhiteSpace(source.UrlTemplate))
            {
                string sampleUrl = source.UrlTemplate
                    .Replace("{date:yyyyMMdd}", "20260101", StringComparison.Ordinal)
                    .Replace("{date:yyyyMM}", "202601", StringComparison.Ordinal)
                    .Replace(
                        "{date:dd%2FMM%2Fyyyy}",
                        "01%2F01%2F2026",
                        StringComparison.Ordinal);
                if (!Uri.TryCreate(sampleUrl, UriKind.Absolute, out Uri? uri) ||
                    uri.Scheme != Uri.UriSchemeHttps)
                {
                    errors.Add($"URL de radar inválida en '{source.Id}'.");
                }
                else if (source.AllowedHosts.Count == 0 ||
                         !source.AllowedHosts.Contains(
                             uri.IdnHost,
                             StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"El host de '{source.Id}' no figura en allowedHosts.");
                }
            }

            if (!string.IsNullOrWhiteSpace(source.FixedMunicipality) &&
                source.SourceKind != OpportunitySourceKind.MunicipalNoticeBoard)
            {
                errors.Add(
                    $"Solo un tablón municipal puede fijar municipio en '{source.Id}'.");
            }

            if (source.SourceKind == OpportunitySourceKind.MunicipalNoticeBoard &&
                string.IsNullOrWhiteSpace(source.FixedMunicipality))
            {
                errors.Add($"El tablón municipal '{source.Id}' debe fijar municipio.");
            }

            if (source.SourceKind == OpportunitySourceKind.OfficialCommercialWebsite &&
                source.Format is not
                    (OpportunityFeedFormat.Sitemap or OpportunityFeedFormat.HtmlLinks))
            {
                errors.Add(
                    $"La fuente comercial '{source.Id}' debe usar Sitemap o HtmlLinks.");
            }

            if (source.Format is
                    OpportunityFeedFormat.Sitemap or OpportunityFeedFormat.HtmlLinks &&
                source.SourceKind != OpportunitySourceKind.OfficialCommercialWebsite)
            {
                errors.Add(
                    $"La fuente '{source.Id}' debe declararse como web comercial oficial.");
            }

            if (source.Format == OpportunityFeedFormat.HtmlLinks &&
                source.ItemSelectors.Count == 0)
            {
                errors.Add(
                    $"El seguimiento de enlaces '{source.Id}' necesita selectores acotados.");
            }

            foreach (OpportunityReviewRule rule in source.ReviewRules)
            {
                if (string.IsNullOrWhiteSpace(rule.UrlPattern))
                {
                    errors.Add(
                        $"Una regla de revisión de '{source.Id}' no tiene patrón de URL.");
                }

                if (rule.Status is
                    OpportunityCandidateStatus.New or
                    OpportunityCandidateStatus.VerifiedSource)
                {
                    errors.Add(
                        $"La regla de revisión de '{source.Id}' no puede asignar " +
                        $"el estado {rule.Status}.");
                }
            }

            if (!string.IsNullOrWhiteSpace(source.FixedMunicipality) &&
                municipalities is not null &&
                !municipalities.Any(municipality =>
                    municipality.Enabled &&
                    string.Equals(
                        municipality.OfficialName,
                        source.FixedMunicipality,
                        StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(
                    $"El tablón '{source.Id}' fija un municipio desconocido o deshabilitado: " +
                    $"'{source.FixedMunicipality}'.");
            }
        }

        return errors;
    }

    public IReadOnlyList<string> Validate(
        CrawlerSettings settings,
        IReadOnlyList<SourceDefinition> sources,
        IReadOnlyList<MunicipalityDefinition> municipalities,
        MunicipalityCentroidCatalog? centroidCatalog = null)
    {
        List<string> errors = [];
        if (settings.TimeoutSeconds < 1 || settings.MaxRetries is < 0 or > 5)
        {
            errors.Add("TimeoutSeconds o MaxRetries están fuera de rango.");
        }

        foreach (IGrouping<string, SourceDefinition> duplicate in sources
                     .GroupBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Id de fuente duplicado: {duplicate.Key}.");
        }

        foreach (SourceDefinition source in sources.Where(source => source.Enabled))
        {
            if (string.IsNullOrWhiteSpace(source.Id) || string.IsNullOrWhiteSpace(source.Name))
            {
                errors.Add("Toda fuente habilitada necesita id y name.");
            }

            if (source.FixturePath is null && source.StartUrls.Count == 0)
            {
                errors.Add($"La fuente '{source.Id}' no tiene startUrls ni fixturePath.");
            }

            foreach (string url in source.StartUrls)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errors.Add($"URL no válida en '{source.Id}': {url}");
                }
            }

            if (!string.IsNullOrWhiteSpace(source.FixedMunicipality) &&
                !municipalities.Any(municipality => string.Equals(
                    municipality.OfficialName,
                    source.FixedMunicipality,
                    StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(
                    $"La fuente '{source.Id}' fija un municipio desconocido: " +
                    $"'{source.FixedMunicipality}'.");
            }
        }

        if (municipalities.Count == 0)
        {
            errors.Add("Debe existir al menos un municipio.");
        }

        foreach (IGrouping<string, MunicipalityDefinition> duplicate in municipalities
                     .GroupBy(item => item.OfficialName, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Municipio duplicado: {duplicate.Key}.");
        }

        ValidateCentroids(municipalities, centroidCatalog, errors);
        return errors;
    }

    private static void ValidateCentroids(
        IReadOnlyList<MunicipalityDefinition> municipalities,
        MunicipalityCentroidCatalog? catalog,
        ICollection<string> errors)
    {
        if (catalog is null)
        {
            return;
        }

        bool knownReferenceSystem =
            string.Equals(
                catalog.CoordinateReferenceSystem,
                "WGS84",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                catalog.CoordinateReferenceSystem,
                "ETRS89",
                StringComparison.OrdinalIgnoreCase);
        if (catalog.SchemaVersion is not ("1.0" or "1.1") ||
            !knownReferenceSystem)
        {
            errors.Add("El catálogo de centroides debe usar el contrato 1.0/1.1 y WGS84/ETRS89.");
        }

        if (catalog.SchemaVersion == "1.1" &&
            (string.IsNullOrWhiteSpace(catalog.DatasetName) ||
             string.IsNullOrWhiteSpace(catalog.DatasetEdition) ||
             string.IsNullOrWhiteSpace(catalog.SourceFile) ||
             catalog.SourceFileSha256.Length != 64 ||
             !catalog.SourceFileSha256.All(Uri.IsHexDigit) ||
             !string.Equals(catalog.License, "CC-BY 4.0", StringComparison.OrdinalIgnoreCase) ||
             string.IsNullOrWhiteSpace(catalog.Attribution)))
        {
            errors.Add(
                "El catálogo 1.1 necesita dataset, edición, fichero, SHA-256, licencia y atribución.");
        }

        foreach (IGrouping<string, MunicipalityCentroidSource> duplicate in catalog.Sources
                     .GroupBy(item => item.Municipality, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Procedencia de centroide duplicada: {duplicate.Key}.");
        }

        Dictionary<string, MunicipalityCentroidSource> provenance = catalog.Sources
            .GroupBy(item => item.Municipality, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        Dictionary<string, MunicipalityDefinition> definitions = municipalities
            .GroupBy(item => item.OfficialName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (MunicipalityDefinition municipality in municipalities)
        {
            bool hasLatitude = municipality.Latitude.HasValue;
            bool hasLongitude = municipality.Longitude.HasValue;
            if (hasLatitude != hasLongitude)
            {
                errors.Add(
                    $"El municipio '{municipality.OfficialName}' debe tener ambas coordenadas o ninguna.");
                continue;
            }

            if (!hasLatitude)
            {
                continue;
            }

            if (municipality.Latitude is < -90 or > 90 ||
                municipality.Longitude is < -180 or > 180)
            {
                errors.Add($"Centroide fuera de rango: {municipality.OfficialName}.");
            }

            if (!provenance.TryGetValue(municipality.OfficialName, out MunicipalityCentroidSource? source))
            {
                errors.Add(
                    $"Falta procedencia para el centroide de '{municipality.OfficialName}'.");
                continue;
            }

            if (municipality.Latitude != source.Latitude ||
                municipality.Longitude != source.Longitude)
            {
                errors.Add(
                    $"Las coordenadas y la procedencia no coinciden para '{municipality.OfficialName}'.");
            }
        }

        foreach (MunicipalityCentroidSource source in catalog.Sources)
        {
            if (!definitions.TryGetValue(source.Municipality, out MunicipalityDefinition? municipality))
            {
                errors.Add(
                    $"La procedencia referencia un municipio desconocido: '{source.Municipality}'.");
                continue;
            }

            if (!municipality.Latitude.HasValue || !municipality.Longitude.HasValue)
            {
                errors.Add(
                    $"La procedencia de '{source.Municipality}' no tiene coordenadas publicadas.");
            }

            if (!Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out Uri? sourceUri) ||
                sourceUri.Scheme != Uri.UriSchemeHttps)
            {
                errors.Add(
                    $"La procedencia de '{source.Municipality}' necesita una URL HTTPS válida.");
            }

            if (source.CheckedAtUtc == default ||
                source.CheckedAtUtc.Offset != TimeSpan.Zero)
            {
                errors.Add(
                    $"La fecha de comprobación de '{source.Municipality}' debe estar en UTC.");
            }

            if (catalog.SchemaVersion == "1.1" &&
                (string.IsNullOrWhiteSpace(source.SourceRecordId) ||
                 string.IsNullOrWhiteSpace(source.CoordinateOrigin)))
            {
                errors.Add(
                    $"La procedencia de '{source.Municipality}' necesita registro y origen de coordenada.");
            }
        }
    }

    private static async Task<IReadOnlyList<T>> LoadListAsync<T>(
        string path,
        string propertyName,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty(propertyName, out JsonElement element))
        {
            throw new InvalidDataException($"El archivo '{path}' no contiene '{propertyName}'.");
        }

        return element.Deserialize<T[]>(JsonDefaults.Compact) ?? [];
    }

    private sealed class SettingsEnvelope
    {
        public CrawlerSettings Crawler { get; init; } = new();
    }
}
