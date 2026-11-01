namespace Guanacaste3D.Core;

/// <summary>A single, simple, wall-bounded room footprint without courtyards/holes.</summary>
public sealed record RoomRegion(IReadOnlyList<Point2> Boundary)
{
    public Point2 Min => new(Boundary.Min(point => point.X), Boundary.Min(point => point.Y));
    public Point2 Max => new(Boundary.Max(point => point.X), Boundary.Max(point => point.Y));
    public double Area => RoomRegionGeometry.Area(Boundary);
}

/// <summary>
/// Shared polygon measurements for persisted room footprints. Independent from any view or WPF
/// library, so drawings, schedules and file round trips measure the same room geometry.
/// </summary>
public static class RoomRegionGeometry
{
    public static double SignedArea(IReadOnlyList<Point2> boundary)
    {
        if (boundary.Count < 3) return 0;
        // Local coordinates avoid cancellation for projects far from the world origin.
        var origin = boundary[0];
        double twice = 0;
        for (var i = 1; i < boundary.Count - 1; i++)
        {
            var a = boundary[i];
            var b = boundary[i + 1];
            twice += (a.X - origin.X) * (b.Y - origin.Y) -
                     (b.X - origin.X) * (a.Y - origin.Y);
        }
        return twice / 2;
    }

    public static double Area(IReadOnlyList<Point2> boundary) => Math.Abs(SignedArea(boundary));

    public static IReadOnlyList<Point2> Outline(Room room)
    {
        if (room.Boundary is { Count: >= 3 } boundary) return boundary;
        var minX = Math.Min(room.FirstCorner.X, room.SecondCorner.X);
        var maxX = Math.Max(room.FirstCorner.X, room.SecondCorner.X);
        var minY = Math.Min(room.FirstCorner.Y, room.SecondCorner.Y);
        var maxY = Math.Max(room.FirstCorner.Y, room.SecondCorner.Y);
        return [new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY)];
    }

    /// <summary>Checks self-intersection and degenerate edges, including nonadjacent edge touches.</summary>
    public static bool IsValid(IReadOnlyList<Point2> polygon)
    {
        if (polygon.Count is < 3 or > 2048 ||
            polygon.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)) ||
            Area(polygon) < 0.1) return false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (a.DistanceTo(b) < 1e-7) return false;
            for (var j = i + 1; j < polygon.Count; j++)
            {
                if (j == i + 1 || i == 0 && j == polygon.Count - 1) continue;
                var c = polygon[j];
                var d = polygon[(j + 1) % polygon.Count];
                if (Intersect(a, b, c, d)) return false;
            }
        }
        return true;
    }

    private static double Cross(Point2 a, Point2 b, Point2 c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool OnSegment(Point2 a, Point2 b, Point2 point) =>
        Math.Abs(Cross(a, b, point)) < 1e-9 &&
        point.X >= Math.Min(a.X, b.X) - 1e-9 && point.X <= Math.Max(a.X, b.X) + 1e-9 &&
        point.Y >= Math.Min(a.Y, b.Y) - 1e-9 && point.Y <= Math.Max(a.Y, b.Y) + 1e-9;

    private static bool Intersect(Point2 a, Point2 b, Point2 c, Point2 d)
    {
        var ac = Cross(a, b, c);
        var ad = Cross(a, b, d);
        var ca = Cross(c, d, a);
        var cb = Cross(c, d, b);
        if ((ac > 0 && ad < 0 || ac < 0 && ad > 0) &&
            (ca > 0 && cb < 0 || ca < 0 && cb > 0)) return true;
        return Math.Abs(ac) < 1e-9 && OnSegment(a, b, c) ||
               Math.Abs(ad) < 1e-9 && OnSegment(a, b, d) ||
               Math.Abs(ca) < 1e-9 && OnSegment(c, d, a) ||
               Math.Abs(cb) < 1e-9 && OnSegment(c, d, b);
    }

    public static bool Contains(IReadOnlyList<Point2> polygon, Point2 point)
    {
        var inside = false;
        for (var i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Centroid, with a guaranteed interior fallback for concave orthogonal footprints.</summary>
    public static Point2 LabelPoint(IReadOnlyList<Point2> polygon)
    {
        var first = polygon[0];
        double twice = 0, x = 0, y = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = new Point2(polygon[i].X - first.X, polygon[i].Y - first.Y);
            var b = new Point2(polygon[(i + 1) % polygon.Count].X - first.X,
                polygon[(i + 1) % polygon.Count].Y - first.Y);
            var cross = a.X * b.Y - b.X * a.Y;
            twice += cross;
            x += (a.X + b.X) * cross;
            y += (a.Y + b.Y) * cross;
        }
        if (Math.Abs(twice) > 1e-8)
        {
            var centroid = new Point2(first.X + x / (3 * twice), first.Y + y / (3 * twice));
            if (Contains(polygon, centroid)) return centroid;
        }

        var xs = polygon.Select(point => point.X).Distinct().OrderBy(value => value).ToArray();
        var ys = polygon.Select(point => point.Y).Distinct().OrderBy(value => value).ToArray();
        var largest = -1d;
        var best = polygon[0];
        for (var i = 0; i + 1 < xs.Length; i++)
        for (var j = 0; j + 1 < ys.Length; j++)
        {
            var midpoint = new Point2((xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2);
            var area = (xs[i + 1] - xs[i]) * (ys[j + 1] - ys[j]);
            if (area > largest && Contains(polygon, midpoint))
            {
                largest = area;
                best = midpoint;
            }
        }
        return best;
    }

    /// <summary>Exact planar intersection area for orthogonal polygons, including concave L-shaped rooms.</summary>
    public static double IntersectionArea(IReadOnlyList<Point2> a, IReadOnlyList<Point2> b)
    {
        var xs = a.Concat(b).Select(point => point.X).Distinct().OrderBy(value => value).ToArray();
        var ys = a.Concat(b).Select(point => point.Y).Distinct().OrderBy(value => value).ToArray();
        var total = 0d;
        for (var i = 0; i + 1 < xs.Length; i++)
        for (var j = 0; j + 1 < ys.Length; j++)
        {
            var center = new Point2((xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2);
            if (Contains(a, center) && Contains(b, center))
                total += (xs[i + 1] - xs[i]) * (ys[j + 1] - ys[j]);
        }
        return total;
    }
}
