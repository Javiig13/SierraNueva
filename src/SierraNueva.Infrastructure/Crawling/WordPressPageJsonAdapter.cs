using System.Net;
using System.Text.Json;

namespace SierraNueva.Infrastructure.Crawling;

internal static class WordPressPageJsonAdapter
{
    public static AdaptedWordPressPage? TryAdapt(string json, Uri requestUri)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? rendered = ReadString(root, "content", "rendered");
            if (string.IsNullOrWhiteSpace(rendered))
            {
                return null;
            }

            string title = ReadString(root, "title", "rendered") ?? string.Empty;
            Uri canonicalUri = ReadCanonicalUri(root, requestUri);
            string encodedTitle = WebUtility.HtmlEncode(WebUtility.HtmlDecode(title));
            string encodedCanonical = WebUtility.HtmlEncode(canonicalUri.AbsoluteUri);
            string html =
                "<!doctype html><html lang=\"es\"><head>" +
                $"<title>{encodedTitle}</title>" +
                $"<link rel=\"canonical\" href=\"{encodedCanonical}\">" +
                $"<meta property=\"og:url\" content=\"{encodedCanonical}\">" +
                "</head><body>" +
                rendered +
                "</body></html>";

            return new(html, canonicalUri);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Uri ReadCanonicalUri(JsonElement root, Uri requestUri)
    {
        if (root.TryGetProperty("link", out JsonElement linkElement) &&
            linkElement.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(linkElement.GetString(), UriKind.Absolute, out Uri? link) &&
            link.Scheme == Uri.UriSchemeHttps &&
            string.Equals(link.IdnHost, requestUri.IdnHost, StringComparison.OrdinalIgnoreCase))
        {
            return link;
        }

        return requestUri;
    }

    private static string? ReadString(
        JsonElement root,
        string parentProperty,
        string childProperty)
    {
        return root.TryGetProperty(parentProperty, out JsonElement parent) &&
               parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(childProperty, out JsonElement child) &&
               child.ValueKind == JsonValueKind.String
            ? child.GetString()
            : null;
    }
}

internal sealed record AdaptedWordPressPage(string Html, Uri CanonicalUri);
