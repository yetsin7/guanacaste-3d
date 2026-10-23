using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>Metric, deterministic takeoff of rectangular slabs.</summary>
public static class SlabSchedule
{
    public static string ToCsv(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        var csv = new StringBuilder();
        csv.AppendLine("Level,SlabId,Width_m,Depth_m,Thickness_m,Area_m2,Volume_m3");
        var levelNames = document.Levels.ToDictionary(l => l.Id, l => l.Name);
        foreach (var slab in document.Slabs.OrderBy(s => s.LevelId).ThenBy(s => s.Id))
        {
            string Number(double n) => n.ToString("0.######", CultureInfo.InvariantCulture);
            string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
            csv.Append(Escape(levelNames[slab.LevelId])).Append(',')
                .Append(slab.Id).Append(',')
                .Append(Number(slab.Width)).Append(',')
                .Append(Number(slab.Depth)).Append(',')
                .Append(Number(slab.Thickness)).Append(',')
                .Append(Number(slab.Area)).Append(',')
                .Append(Number(slab.Volume)).AppendLine();
        }
        return csv.ToString();
    }
}
