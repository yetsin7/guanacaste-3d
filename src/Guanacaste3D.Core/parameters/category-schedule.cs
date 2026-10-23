using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>Identifies a schedule column; built-in columns have no parameter definition.</summary>
public sealed record CategoryScheduleColumn(string Key, ParameterDefinition? Definition)
{
    /// <summary>Returns whether cells in this column edit a parameter value.</summary>
    public bool IsEditable => Definition is not null;
}

/// <summary>One schedule row: the element plus the effective value for each parameter column.</summary>
public sealed record CategoryScheduleRow(ElementEntry Element, string LevelName,
    IReadOnlyDictionary<Guid, ParameterValue> Values);

/// <summary>A category schedule computed from the current document state.</summary>
public sealed record CategorySchedule(ElementCategory Category,
    IReadOnlyList<CategoryScheduleColumn> Columns, IReadOnlyList<CategoryScheduleRow> Rows);

/// <summary>
/// Builds element-per-row schedules for one category with a column per bound
/// project parameter. Because rows are derived from the document, any model
/// edit, undo, or redo produces an up-to-date schedule on the next build.
/// </summary>
public static class CategoryScheduleService
{
    public const string MarkColumn = "Mark";
    public const string NameColumn = "Name";
    public const string LevelColumn = "Level";

    /// <summary>Builds the schedule for a category, resolving type-bound values through each element's type.</summary>
    public static CategorySchedule Build(BuildingDocument document, ElementCategory category)
    {
        var definitions = ParameterRules.ForCategory(document, category);
        var columns = new List<CategoryScheduleColumn>
        {
            new(MarkColumn, null), new(NameColumn, null), new(LevelColumn, null)
        };
        columns.AddRange(definitions.Select(definition => new CategoryScheduleColumn(
            definition.Id.ToString("N"), definition)));
        var stored = document.ParameterValues.ToDictionary(value => (value.DefinitionId, value.OwnerId));
        var levelNames = document.Levels.ToDictionary(level => level.Id, level => level.Name);
        var rows = ElementCatalog.List(document, category).Select(element =>
        {
            var values = new Dictionary<Guid, ParameterValue>();
            foreach (var definition in definitions)
            {
                if (ParameterRules.ResolveOwner(definition, element) is Guid ownerId &&
                    stored.TryGetValue((definition.Id, ownerId), out var value))
                    values[definition.Id] = value;
            }
            var levelName = element.LevelId is Guid levelId ? levelNames.GetValueOrDefault(levelId, "") : "";
            return new CategoryScheduleRow(element, levelName, values);
        }).ToArray();
        return new CategorySchedule(category, columns, rows);
    }

    /// <summary>Exports a schedule as RFC 4180 CSV with invariant numbers and unit-labelled headers.</summary>
    public static string ToCsv(CategorySchedule schedule)
    {
        var csv = new StringBuilder();
        csv.AppendJoin(',', schedule.Columns.Select(column => Escape(column.Definition is { } definition
            ? HeaderWithUnit(definition)
            : column.Key))).AppendLine();
        foreach (var row in schedule.Rows)
        {
            var cells = schedule.Columns.Select(column => column.Key switch
            {
                MarkColumn => row.Element.Mark,
                NameColumn => row.Element.Name,
                LevelColumn => row.LevelName,
                _ => row.Values.TryGetValue(column.Definition!.Id, out var value)
                    ? ParameterValueText.Format(column.Definition.Spec, value, CultureInfo.InvariantCulture)
                    : string.Empty
            });
            csv.AppendJoin(',', cells.Select(Escape)).AppendLine();
        }
        return csv.ToString();
    }

    /// <summary>Appends the display unit to a parameter header, such as "Finish height (m)".</summary>
    private static string HeaderWithUnit(ParameterDefinition definition)
    {
        var unit = ParameterValueText.UnitSuffix(definition.Spec);
        return unit.Length == 0 ? definition.Name : $"{definition.Name} ({unit})";
    }

    /// <summary>Quotes a CSV cell and neutralizes spreadsheet formula prefixes.</summary>
    private static string Escape(string value)
    {
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) && !double.TryParse(value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
