using System.Globalization;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// Accent-insensitive, order-independent search for Spanish material names, stable ids
/// and localized material families. All words must occur in at least one supplied field.
/// </summary>
public static class MaterialCatalogSearch
{
    private static readonly CompareInfo Spanish = CultureInfo.GetCultureInfo("es-ES").CompareInfo;
    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Matches all search terms across names, identifiers, and localized categories.</summary>
    public static bool Matches(string? query, params string?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (string.IsNullOrWhiteSpace(query)) return true;

        var normalizedQuery = query.Replace('-', ' ').Replace('_', ' ');
        var words = normalizedQuery.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length > 0 && words.All(word => fields.Any(field =>
            !string.IsNullOrEmpty(field) && Spanish.IndexOf(field, word, SearchOptions) >= 0));
    }
}
