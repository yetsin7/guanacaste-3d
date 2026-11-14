namespace Guanacaste3D.Core;

/// <summary>
/// Names a new grid line the way the reference application does: it continues the sequence of the last grid drawn
/// (A → B, 3 → 4, A1 → A2), so renaming the first grid of a row to "A" letters the rest. When the follower is taken,
/// or there is no grid yet, it falls back to the first free number.
/// </summary>
public static class GridAxisNaming
{
    public static string Next(IReadOnlyList<GridAxis> grids)
    {
        ArgumentNullException.ThrowIfNull(grids);
        var used = grids.Select(axis => axis.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (grids.Count > 0 && Successor(grids[^1].Name.Trim()) is { } follower && !used.Contains(follower)) return follower;
        var number = 1;
        while (used.Contains(number.ToString(System.Globalization.CultureInfo.InvariantCulture))) number++;
        return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The name after <paramref name="name"/>: its trailing number plus one, or its trailing letters advanced (Z → AA).</summary>
    public static string? Successor(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var digits = name.Length;
        while (digits > 0 && char.IsAsciiDigit(name[digits - 1])) digits--;
        if (digits < name.Length)
        {
            var tail = name[digits..];
            if (!long.TryParse(tail, out var value) || value == long.MaxValue) return null;
            // Keep leading zeros: 01 → 02.
            return name[..digits] + (value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(tail.Length, '0');
        }
        var letters = name.Length;
        while (letters > 0 && char.IsAsciiLetter(name[letters - 1])) letters--;
        if (letters == name.Length) return null;
        var chars = name[letters..].ToCharArray();
        for (var index = chars.Length - 1; index >= 0; index--)
        {
            var upper = char.IsAsciiLetterUpper(chars[index]);
            var last = upper ? 'Z' : 'z';
            if (chars[index] != last)
            {
                chars[index]++;
                return name[..letters] + new string(chars);
            }
            chars[index] = upper ? 'A' : 'a';
        }
        return name[..letters] + (char.IsAsciiLetterUpper(name[letters]) ? "A" : "a") + new string(chars);
    }
}
