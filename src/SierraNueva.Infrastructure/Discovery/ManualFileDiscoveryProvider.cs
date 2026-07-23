using System.Text.Json;
using SierraNueva.Contracts;
using SierraNueva.Core.Abstractions;

namespace SierraNueva.Infrastructure.Discovery;

public sealed class ManualFileDiscoveryProvider : IUrlDiscoveryProvider
{
    public async Task<IReadOnlyList<Uri>> DiscoverAsync(
        SourceDefinition source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.ManualUrlsFile))
        {
            return [];
        }

        string path = Path.GetFullPath(source.ManualUrlsFile, Directory.GetCurrentDirectory());
        if (!File.Exists(path))
        {
            return [];
        }

        IReadOnlyList<string> values = Path.GetExtension(path).Equals(
            ".json",
            StringComparison.OrdinalIgnoreCase)
            ? await ReadJsonAsync(path, cancellationToken)
            : await File.ReadAllLinesAsync(path, cancellationToken);
        return values
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => Uri.TryCreate(line, UriKind.Absolute, out Uri? uri) ? uri : null)
            .Where(uri => uri is not null)
            .Cast<Uri>()
            .ToArray();
    }

    private static async Task<IReadOnlyList<string>> ReadJsonAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        JsonElement urls = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("urls", out JsonElement property)
                ? property
                : default;
        return urls.ValueKind == JsonValueKind.Array
            ? urls.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray()
            : [];
    }
}
