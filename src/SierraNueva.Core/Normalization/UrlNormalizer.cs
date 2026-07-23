namespace SierraNueva.Core.Normalization;

public static class UrlNormalizer
{
    private static readonly HashSet<string> TrackingParameters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "fbclid",
            "gclid",
            "mc_cid",
            "mc_eid",
            "ref",
            "source"
        };

    public static string Normalize(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return url.Trim();
        }

        UriBuilder builder = new(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1,
            Host = uri.IdnHost.ToLowerInvariant(),
            Fragment = string.Empty
        };

        List<KeyValuePair<string, string>> parameters = ParseQuery(uri.Query)
            .Where(pair => !pair.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
            .Where(pair => !TrackingParameters.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToList();

        builder.Query = string.Join(
            "&",
            parameters.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        builder.Path = builder.Path == "/" ? "/" : builder.Path.TrimEnd('/');
        return builder.Uri.AbsoluteUri;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        foreach (string segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = segment.Split('=', 2);
            yield return new(
                Uri.UnescapeDataString(parts[0]),
                parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
        }
    }
}
