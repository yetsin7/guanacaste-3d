namespace Guanacaste3D.Core;

/// <summary>
/// One thing standing in the clear space in front of a door: the door, what stands there and how much space the door
/// needs on each side (meters).
/// </summary>
public sealed record DoorObstruction(Guid OpeningId, Guid ObstructionId, double RequiredDepth);

/// <summary>
/// The rule that nothing may stand in front of a door. Each side of a door keeps a clear rectangle as wide as the door
/// leaf (plus 5 cm each side) and as deep as the leaf is wide, never less than 0.90 m, so the leaf can swing and a
/// person can reach it. A column, a piece of furniture or a stair inside that rectangle is reported; in a real building
/// it could not be built that way.
/// </summary>
public static class DoorClearance
{
    /// <summary>The least clear depth in front of a door, in meters.</summary>
    public const double MinimumDepth = 0.90;

    /// <summary>Extra clear width beyond each jamb, in meters.</summary>
    public const double JambMargin = 0.05;

    /// <summary>The clear zone on one side of a door (<paramref name="side"/> is 1 or -1 along the wall's left normal).</summary>
    public static IReadOnlyList<Point2> Zone(Wall wall, WallOpening opening, int side)
    {
        var length = wall.Length;
        if (length < 1e-9) return [];
        var (dx, dy) = ((wall.End.X - wall.Start.X) / length, (wall.End.Y - wall.Start.Y) / length);
        var (nx, ny) = (-dy * side, dx * side);
        var depth = Math.Max(MinimumDepth, opening.Width);
        var from = opening.Begin - JambMargin;
        var to = opening.End + JambMargin;
        var near = wall.Thickness / 2;
        Point2 At(double along, double across) => new(wall.Start.X + dx * along + nx * across, wall.Start.Y + dy * along + ny * across);
        return [At(from, near), At(to, near), At(to, near + depth), At(from, near + depth)];
    }

    /// <summary>Everything that stands in front of a door of the document.</summary>
    public static IReadOnlyList<DoorObstruction> Analyze(BuildingDocument document, Guid? onlyElement = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new List<DoorObstruction>();
        var walls = document.Walls.ToDictionary(wall => wall.Id);
        var blockers = Blockers(document, onlyElement);
        if (blockers.Count == 0) return result;
        foreach (var door in document.Openings.Where(opening => opening.Kind == OpeningKind.Door))
        {
            if (!walls.TryGetValue(door.WallId, out var wall)) continue;
            var required = Math.Max(MinimumDepth, door.Width);
            foreach (var side in new[] { 1, -1 })
            {
                var zone = Zone(wall, door, side);
                if (zone.Count < 3) continue;
                foreach (var blocker in blockers.Where(item => item.LevelId == wall.LevelId && item.Bottom < door.Height))
                    if (Overlap(zone, blocker.Outline) && !result.Any(found => found.OpeningId == door.Id && found.ObstructionId == blocker.Id))
                        result.Add(new DoorObstruction(door.Id, blocker.Id, required));
            }
        }
        return result;
    }

    private sealed record Blocker(Guid Id, Guid LevelId, IReadOnlyList<Point2> Outline, double Bottom);

    private static List<Blocker> Blockers(BuildingDocument document, Guid? only)
    {
        var list = new List<Blocker>();
        foreach (var column in document.Columns.Where(item => only is null || item.Id == only))
            list.Add(new Blocker(column.Id, column.LevelId, column.Outline, 0));
        foreach (var element in document.StructuralElements.Where(item => only is null || item.Id == only))
        {
            if (element.Kind is not (StructuralElementKind.Component or StructuralElementKind.Stair)) continue;
            if (element.Kind == StructuralElementKind.Component && element.FamilyId is null && element.Width < 0.2) continue;
            var outline = element.Footprint;
            if (outline.Count >= 3) list.Add(new Blocker(element.Id, element.LevelId, outline, Math.Max(0, element.Offset)));
        }
        return list;
    }

    /// <summary>Whether two convex polygons share area (touching edges do not count), by the separating-axis test.</summary>
    private static bool Overlap(IReadOnlyList<Point2> a, IReadOnlyList<Point2> b)
    {
        const double tolerance = 1e-4;
        foreach (var polygon in new[] { a, b })
            for (var index = 0; index < polygon.Count; index++)
            {
                var p = polygon[index];
                var q = polygon[(index + 1) % polygon.Count];
                var (ax, ay) = (-(q.Y - p.Y), q.X - p.X);
                var (minA, maxA) = Project(a, ax, ay);
                var (minB, maxB) = Project(b, ax, ay);
                if (maxA <= minB + tolerance || maxB <= minA + tolerance) return false;
            }
        return true;
    }

    private static (double Min, double Max) Project(IReadOnlyList<Point2> polygon, double ax, double ay)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (var point in polygon)
        {
            var value = point.X * ax + point.Y * ay;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        return (min, max);
    }
}
