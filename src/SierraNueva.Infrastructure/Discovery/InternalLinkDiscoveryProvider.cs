using AngleSharp.Dom;
using SierraNueva.Contracts;

namespace SierraNueva.Infrastructure.Discovery;

public sealed class InternalLinkDiscoveryProvider
{
    private static readonly string[] IrrelevantTerms =
    [
        "login",
        "logout",
        "carrito",
        "cart",
        "cookies",
        "privacidad",
        "privacy",
        "legal",
        "calendar",
        "wp-admin"
    ];

    private static readonly string[] RelevantTerms =
    [
        "promocion",
        "obra-nueva",
        "obra_nueva",
        "residencial",
        "viviendas",
        "chalets",
        "adosados",
        "pareados",
        "unifamiliares",
        "proyectos",
        "proximamente",
        "nueva-fase",
        "cooperativa"
    ];

    public IReadOnlyList<Uri> Discover(
        IDocument document,
        Uri pageUrl,
        SourceDefinition source)
    {
        return document.QuerySelectorAll("a[href]")
            .Select(anchor => anchor.GetAttribute("href"))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(href => Uri.TryCreate(pageUrl, href, out Uri? uri) ? uri : null)
            .Where(uri => uri is not null)
            .Cast<Uri>()
            .Where(uri => uri.Scheme is "http" or "https")
            .Where(uri => source.AllowedHosts.Count == 0 ||
                          source.AllowedHosts.Any(host =>
                              uri.IdnHost.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                              uri.IdnHost.EndsWith(
                                  $".{host.TrimStart('.')}",
                                  StringComparison.OrdinalIgnoreCase)))
            .Where(uri => !IrrelevantTerms.Any(term =>
                uri.PathAndQuery.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Where(uri => source.IncludePatterns.Count == 0 ||
                          source.IncludePatterns.Any(pattern =>
                              uri.PathAndQuery.Contains(pattern, StringComparison.OrdinalIgnoreCase)) ||
                          RelevantTerms.Any(term =>
                              uri.PathAndQuery.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                          uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(uri => !source.ExcludePatterns.Any(pattern =>
                uri.PathAndQuery.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.Ordinal)
            .OrderByDescending(uri => RelevantTerms.Count(term =>
                uri.PathAndQuery.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(uri => uri.AbsoluteUri, StringComparer.Ordinal)
            .ToArray();
    }
}
