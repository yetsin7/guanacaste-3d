using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>
/// Exports a schematic, metric DXF R12 floor-plan drawing without external CAD dependencies.
/// This is a geometric interchange format, not a round-trip BIM serialization.
/// </summary>
public static class DxfExporter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string ToAscii(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        var output = new StringBuilder();
        Pair(output, 0, "SECTION");
        Pair(output, 2, "HEADER");
        Pair(output, 9, "$ACADVER");
        Pair(output, 1, "AC1009");
        Pair(output, 0, "ENDSEC");

        Pair(output, 0, "SECTION");
        Pair(output, 2, "TABLES");
        // Some R12 readers expect the line type table to precede LAYER.
        Pair(output, 0, "TABLE");
        Pair(output, 2, "LTYPE");
        Pair(output, 70, "1");
        Pair(output, 0, "LTYPE");
        Pair(output, 2, "CONTINUOUS");
        Pair(output, 70, "0");
        Pair(output, 3, "Solid line");
        Pair(output, 72, "65");
        Pair(output, 73, "0");
        Pair(output, 40, "0");
        Pair(output, 0, "ENDTAB");

        Pair(output, 0, "TABLE");
        Pair(output, 2, "LAYER");
        Pair(output, 70, (document.Levels.Count * 3 + 1).ToString(Invariant));
        WriteLayer(output, "0", 7);
        for (var i = 0; i < document.Levels.Count; i++)
        {
            WriteLayer(output, Layer(i, "WALL"), 7);
            WriteLayer(output, Layer(i, "DOOR"), 1);
            WriteLayer(output, Layer(i, "WINDOW"), 5);
        }
        Pair(output, 0, "ENDTAB");
        Pair(output, 0, "ENDSEC");

        Pair(output, 0, "SECTION");
        Pair(output, 2, "BLOCKS");
        Pair(output, 0, "ENDSEC");

        Pair(output, 0, "SECTION");
        Pair(output, 2, "ENTITIES");
        for (var i = 0; i < document.Levels.Count; i++)
        {
            var level = document.Levels[i];
            foreach (var wall in document.Walls.Where(w => w.LevelId == level.Id).OrderBy(w => w.Id))
            {
                var openings = document.Openings.Where(o => o.WallId == wall.Id)
                    .OrderBy(o => o.Begin).ThenBy(o => o.Id).ToArray();
                var previous = 0d;
                foreach (var opening in openings)
                {
                    if (opening.Begin > previous)
                        WriteWallRectangle(output, wall, previous, opening.Begin,
                            level.Elevation, Layer(i, "WALL"));
                    WriteEntityLine(output, Along(wall, opening.Begin, 0),
                        Along(wall, opening.End, 0), level.Elevation,
                        Layer(i, opening.Kind == OpeningKind.Door ? "DOOR" : "WINDOW"));
                    previous = opening.End;
                }
                if (previous < wall.Length)
                    WriteWallRectangle(output, wall, previous, wall.Length,
                        level.Elevation, Layer(i, "WALL"));
            }
        }
        Pair(output, 0, "ENDSEC");
        Pair(output, 0, "EOF");
        return output.ToString();
    }

    private static string Layer(int index, string type) => $"G3D-L{index + 1:D3}-{type}";

    private static void WriteLayer(StringBuilder output, string name, int color)
    {
        Pair(output, 0, "LAYER");
        Pair(output, 2, name);
        Pair(output, 70, "0");
        Pair(output, 62, color.ToString(Invariant));
        Pair(output, 6, "CONTINUOUS");
    }

    private static Point2 Along(Wall wall, double distance, double halfThickness)
    {
        var dx = (wall.End.X - wall.Start.X) / wall.Length;
        var dy = (wall.End.Y - wall.Start.Y) / wall.Length;
        return new Point2(
            wall.Start.X + dx * distance - dy * halfThickness * wall.Thickness,
            wall.Start.Y + dy * distance + dx * halfThickness * wall.Thickness);
    }

    private static void WriteWallRectangle(StringBuilder output, Wall wall,
        double start, double end, double elevation, string layer)
    {
        var a = Along(wall, start, 0.5);
        var b = Along(wall, end, 0.5);
        var c = Along(wall, end, -0.5);
        var d = Along(wall, start, -0.5);
        WriteEntityLine(output, a, b, elevation, layer);
        WriteEntityLine(output, b, c, elevation, layer);
        WriteEntityLine(output, c, d, elevation, layer);
        WriteEntityLine(output, d, a, elevation, layer);
    }

    private static void WriteEntityLine(StringBuilder output, Point2 start, Point2 end,
        double elevation, string layer)
    {
        Pair(output, 0, "LINE");
        Pair(output, 8, layer);
        Pair(output, 10, Number(start.X));
        Pair(output, 20, Number(start.Y));
        Pair(output, 30, Number(elevation));
        Pair(output, 11, Number(end.X));
        Pair(output, 21, Number(end.Y));
        Pair(output, 31, Number(elevation));
    }

    private static string Number(double value) => value.ToString("0.########", Invariant);

    private static void Pair(StringBuilder output, int code, string value) =>
        output.Append(code.ToString(Invariant)).Append("\r\n")
            .Append(value).Append("\r\n");
}
