namespace SierraNueva.Infrastructure.Crawling;

public sealed class RobotsRules
{
    private readonly List<string> _disallowed;

    private RobotsRules(List<string> disallowed, IReadOnlyList<Uri> sitemaps)
    {
        _disallowed = disallowed;
        Sitemaps = sitemaps;
    }

    public IReadOnlyList<Uri> Sitemaps { get; }

    public bool IsAllowed(Uri url)
    {
        string path = url.PathAndQuery;
        return !_disallowed.Any(rule =>
            rule.Length > 0 && path.StartsWith(rule, StringComparison.Ordinal));
    }

    public static RobotsRules Parse(string content, Uri baseUri)
    {
        List<string> disallowed = [];
        List<Uri> sitemaps = [];
        bool appliesToAll = false;

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Split('#', 2)[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            string key = parts[0].Trim();
            string value = parts[1].Trim();
            if (key.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
            {
                appliesToAll = value == "*";
            }
            else if (appliesToAll &&
                     key.Equals("disallow", StringComparison.OrdinalIgnoreCase) &&
                     value.Length > 0)
            {
                disallowed.Add(value);
            }
            else if (key.Equals("sitemap", StringComparison.OrdinalIgnoreCase) &&
                     Uri.TryCreate(baseUri, value, out Uri? sitemap))
            {
                sitemaps.Add(sitemap);
            }
        }

        return new(disallowed, sitemaps);
    }

    public static RobotsRules AllowAll { get; } = new([], []);
}
