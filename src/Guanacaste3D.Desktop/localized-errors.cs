using System.Text.RegularExpressions;
using System.Windows;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Translates Core validation messages, which are English by design so the
/// core stays UI-independent, into the active UI language. Messages are looked
/// up as "Error." + message in the merged resource dictionaries; formula
/// errors keep their position suffix; unknown messages pass through unchanged.
/// </summary>
public static partial class LocalizedErrors
{
    /// <summary>Returns the localized text for a Core exception message.</summary>
    public static string Translate(string message)
    {
        if (Application.Current?.TryFindResource("Error." + message) is string direct) return direct;
        var position = FormulaPosition().Match(message);
        if (position.Success &&
            Application.Current?.TryFindResource("Error.Formula." + position.Groups[1].Value) is string formula &&
            Application.Current.TryFindResource("Error.FormulaPosition") is string suffix)
            return formula + string.Format(System.Globalization.CultureInfo.CurrentCulture, suffix, position.Groups[2].Value);
        return message;
    }

    /// <summary>Matches "&lt;reason&gt; at position N of the formula." produced by the formula parser.</summary>
    [GeneratedRegex(@"^(.*) at position (\d+) of the formula\.$")]
    private static partial Regex FormulaPosition();
}
