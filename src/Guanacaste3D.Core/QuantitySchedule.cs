using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>Deterministic, culture-invariant quantity takeoff for wall assemblies.</summary>
public static class QuantitySchedule
{
    public static string ToCsv(BuildingDocument document)
    {
        document.Validate();
        var csv = new StringBuilder();
        csv.AppendLine("Level,WallId,Length_m,Thickness_m,Height_m,GrossVolume_m3,NetVolume_m3,DoorCount,WindowCount");
        foreach (var wall in document.Walls.OrderBy(w => w.LevelId).ThenBy(w => w.Id))
        {
            var level = document.Levels.Single(l => l.Id == wall.LevelId);
            var openings = document.Openings.Where(o => o.WallId == wall.Id).ToArray();
            string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
            string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
            csv.Append(Escape(level.Name)).Append(',')
                .Append(wall.Id).Append(',')
                .Append(Number(wall.Length)).Append(',')
                .Append(Number(wall.Thickness)).Append(',')
                .Append(Number(wall.Height)).Append(',')
                .Append(Number(wall.Length * wall.Thickness * wall.Height)).Append(',')
                .Append(Number(WallGeometry.NetVolume(wall, openings))).Append(',')
                .Append(openings.Count(o => o.Kind == OpeningKind.Door)).Append(',')
                .Append(openings.Count(o => o.Kind == OpeningKind.Window)).AppendLine();
        }
        return csv.ToString();
    }
}
