using SierraNueva.Contracts;

namespace SierraNueva.Core.Normalization;

public sealed class MunicipalityCatalog
{
    private readonly IReadOnlyList<MunicipalityDefinition> _municipalities;
    private readonly Dictionary<string, MunicipalityDefinition> _aliases;

    public MunicipalityCatalog(IReadOnlyList<MunicipalityDefinition> municipalities)
    {
        _municipalities = municipalities;
        _aliases = new(StringComparer.Ordinal);

        foreach (MunicipalityDefinition municipality in municipalities)
        {
            Register(municipality.OfficialName, municipality);
            foreach (string alias in municipality.Aliases)
            {
                Register(alias, municipality);
            }

            foreach (string locality in municipality.Localities)
            {
                Register(locality, municipality);
            }
        }
    }

    public string? ResolveOfficialName(string? text)
    {
        string normalized = TextNormalizer.NormalizeForComparison(text);
        if (_aliases.TryGetValue(normalized, out MunicipalityDefinition? exact))
        {
            return exact.OfficialName;
        }

        MunicipalityDefinition? contained = _aliases
            .Where(entry => normalized.Contains(entry.Key, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Key.Length)
            .Select(entry => entry.Value)
            .FirstOrDefault();

        return contained?.OfficialName;
    }

    public MunicipalityDefinition? Find(string? name)
    {
        string normalized = TextNormalizer.NormalizeForComparison(name);
        return _municipalities.FirstOrDefault(municipality =>
            TextNormalizer.NormalizeForComparison(municipality.OfficialName) == normalized);
    }

    private void Register(string value, MunicipalityDefinition municipality)
    {
        string normalized = TextNormalizer.NormalizeForComparison(value);
        if (normalized.Length > 0)
        {
            _aliases[normalized] = municipality;
        }
    }
}
