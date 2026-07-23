using System.Text.Json;
using SierraNueva.Contracts;
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

    public IReadOnlyList<string> Validate(
        CrawlerSettings settings,
        IReadOnlyList<SourceDefinition> sources,
        IReadOnlyList<MunicipalityDefinition> municipalities)
    {
        List<string> errors = [];
        if (settings.MaxConcurrencyGlobal < 1 || settings.MaxConcurrencyPerHost < 1)
        {
            errors.Add("Los límites de concurrencia deben ser mayores que cero.");
        }

        if (settings.MaxConcurrencyPerHost > settings.MaxConcurrencyGlobal)
        {
            errors.Add("MaxConcurrencyPerHost no puede superar MaxConcurrencyGlobal.");
        }

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

        return errors;
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
