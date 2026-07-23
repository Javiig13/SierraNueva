using System.Text.Json;
using SierraNueva.Infrastructure.Serialization;

namespace SierraNueva.Infrastructure.Crawling;

public sealed class FileHttpMetadataCache : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, HttpMetadata> _entries;

    public FileHttpMetadataCache(string stateDirectory)
    {
        _path = Path.Combine(stateDirectory, "http-cache.json");
        _entries = Load(_path);
    }

    public HttpMetadata? Get(Uri uri)
    {
        return _entries.TryGetValue(uri.AbsoluteUri, out HttpMetadata? metadata)
            ? metadata
            : null;
    }

    public async Task SetAsync(
        Uri uri,
        string? etag,
        DateTimeOffset? lastModified,
        CancellationToken cancellationToken)
    {
        if (etag is null && lastModified is null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _entries[uri.AbsoluteUri] = new()
            {
                ETag = etag,
                LastModifiedUtc = lastModified,
                CheckedAtUtc = DateTimeOffset.UtcNow
            };
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(_entries, JsonDefaults.Indented),
                cancellationToken);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Dictionary<string, HttpMetadata> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, HttpMetadata>>(
                       File.ReadAllText(path),
                       JsonDefaults.Compact)
                   ?? new(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}

public sealed class HttpMetadata
{
    public string? ETag { get; init; }

    public DateTimeOffset? LastModifiedUtc { get; init; }

    public DateTimeOffset CheckedAtUtc { get; init; }
}
