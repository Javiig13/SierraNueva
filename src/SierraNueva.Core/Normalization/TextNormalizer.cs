using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SierraNueva.Core.Normalization;

public static partial class TextNormalizer
{
    public static string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);

        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }
        }

        return WhitespaceRegex().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    public static string CleanEvidence(string? value, int maxLength = 280)
    {
        string clean = WhitespaceRegex().Replace(value ?? string.Empty, " ").Trim();
        return clean.Length <= maxLength ? clean : string.Concat(clean.AsSpan(0, maxLength - 1), "…");
    }

    public static string NormalizeCompanyName(string? value)
    {
        string normalized = NormalizeForComparison(value);
        string[] suffixes = [" s l", " sl", " s a", " sa", " sociedad limitada", " sociedad anonima"];
        foreach (string suffix in suffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                normalized = normalized[..^suffix.Length].Trim();
            }
        }

        return normalized;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
