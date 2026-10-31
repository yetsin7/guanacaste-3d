using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>Deterministic, metric roof quantities for independent estimating and auditing.</summary>
public static class RoofQuantitySchedule
{
    /// <summary>
    /// Exports one row per roof, distinguishing authored plan area, built footprint and true top surface.
    /// The projected perimeter is not a gutter or sloping trim quantity. Roof names are appended
    /// after the original columns to retain their order for existing consumers.
    /// </summary>
    public static string ToCsv(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        var csv = new StringBuilder();
        csv.AppendLine("Level,RoofId,Shape,Pitch_deg,Overhang_m,PlanArea_m2,CoveredPlanArea_m2,SurfaceArea_m2,ProjectedPerimeter_m,RoofName");
        foreach (var roof in document.Floors.Where(floor => floor.Kind == FloorKind.Roof)
                     .OrderBy(roof => roof.LevelId).ThenBy(roof => roof.Id))
        {
            var level = document.Levels.Single(item => item.Id == roof.LevelId);
            var built = RoofGeometry.WithOverhang(roof);
            csv.Append(CsvText(level.Name)).Append(',')
                .Append(roof.Id).Append(',')
                .Append(roof.Shape).Append(',')
                .Append(Number(roof.Pitch)).Append(',')
                .Append(Number(roof.Overhang)).Append(',')
                .Append(Number(roof.CalculateArea())).Append(',')
                .Append(Number(built.CalculateArea())).Append(',')
                .Append(Number(RoofGeometry.SurfaceArea(roof))).Append(',')
                .Append(Number(RoofGeometry.ProjectedPerimeter(roof))).Append(',')
                .Append(CsvText(roof.Name)).AppendLine();
        }
        return csv.ToString();
    }

    /// <summary>
    /// Quotes CSV text and prevents spreadsheet formula execution for user-controlled labels.
    /// A leading apostrophe is added only when the first non-whitespace character could
    /// start a formula in common spreadsheet programs.
    /// </summary>
    private static string CsvText(string value)
    {
        var trimmed = value.AsSpan().TrimStart();
        var formula = !trimmed.IsEmpty && trimmed[0] is '=' or '+' or '-' or '@';
        var safe = formula ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private static string Number(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
