namespace Guanacaste3D.Core;

/// <summary>
/// Discovers rooms whose contours are rectilinear, including concave L- and U-shaped plans.
/// A flood fill groups cells, then exposed cell edges are joined into a polygon in model space.
/// Walls offset each exposed edge inward by half their thickness; room separators have zero thickness.
/// </summary>
public static class RoomPolygonDetection
{
    private const double Tolerance = 1e-3;
    private const int MaximumCells = 20000;

    private sealed record Edge(int FromX, int FromY, int ToX, int ToY, double Offset)
    {
        public bool Horizontal => FromY == ToY;
        public (int X, int Y) From => (FromX, FromY);
        public (int X, int Y) To => (ToX, ToY);
    }

    private static List<double> Coordinates(IEnumerable<double> source)
    {
        var result = new List<double>();
        foreach (var value in source.OrderBy(value => value))
            if (result.Count == 0 || value - result[^1] > Tolerance) result.Add(value);
        return result;
    }

    public static IReadOnlyList<RoomRegion> Detect(BuildingDocument document, Guid levelId)
    {
        var walls = document.Walls.Where(wall => wall.LevelId == levelId &&
            (Math.Abs(wall.Start.X - wall.End.X) < Tolerance ||
             Math.Abs(wall.Start.Y - wall.End.Y) < Tolerance)).ToArray();
        var separators = document.StructuralElements.Where(element =>
            element.LevelId == levelId && element.Kind == StructuralElementKind.RoomSeparator &&
            (Math.Abs(element.Start.X - element.End.X) < Tolerance ||
             Math.Abs(element.Start.Y - element.End.Y) < Tolerance)).ToArray();
        if (walls.Length < 3) return [];
        var bounds = walls.Select(wall => (wall.Start, wall.End, Half: wall.Thickness / 2))
            .Concat(separators.Select(element => (element.Start, element.End, Half: 0d))).ToArray();
        var xs = Coordinates(bounds.SelectMany(bound => new[] { bound.Start.X, bound.End.X }));
        var ys = Coordinates(bounds.SelectMany(bound => new[] { bound.Start.Y, bound.End.Y }));
        if (xs.Count < 2 || ys.Count < 2) return [];
        var nx = xs.Count - 1;
        var ny = ys.Count - 1;
        if ((long)nx * ny > MaximumCells)
            throw new InvalidDataException("The room boundary grid is too large.");

        var vertical = new bool[nx + 1, ny];
        var verticalOffset = new double[nx + 1, ny];
        var horizontal = new bool[nx, ny + 1];
        var horizontalOffset = new double[nx, ny + 1];

        for (var x = 0; x <= nx; x++)
        for (var y = 0; y < ny; y++)
            foreach (var segment in bounds)
                if (Math.Abs(segment.Start.X - segment.End.X) < Tolerance &&
                    Math.Abs(segment.Start.X - xs[x]) < Tolerance &&
                    Math.Min(segment.Start.Y, segment.End.Y) <= ys[y] + Tolerance &&
                    Math.Max(segment.Start.Y, segment.End.Y) >= ys[y + 1] - Tolerance)
                {
                    vertical[x, y] = true;
                    verticalOffset[x, y] = Math.Max(verticalOffset[x, y], segment.Half);
                }

        for (var x = 0; x < nx; x++)
        for (var y = 0; y <= ny; y++)
            foreach (var segment in bounds)
                if (Math.Abs(segment.Start.Y - segment.End.Y) < Tolerance &&
                    Math.Abs(segment.Start.Y - ys[y]) < Tolerance &&
                    Math.Min(segment.Start.X, segment.End.X) <= xs[x] + Tolerance &&
                    Math.Max(segment.Start.X, segment.End.X) >= xs[x + 1] - Tolerance)
                {
                    horizontal[x, y] = true;
                    horizontalOffset[x, y] = Math.Max(horizontalOffset[x, y], segment.Half);
                }

        var visited = new bool[nx, ny];
        var rooms = new List<RoomRegion>();
        for (var x = 0; x < nx; x++)
        for (var y = 0; y < ny; y++)
        {
            if (visited[x, y]) continue;
            var cells = new HashSet<int>();
            var reachesOutside = false;
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((x, y));
            visited[x, y] = true;
            while (queue.Count != 0)
            {
                var (cx, cy) = queue.Dequeue();
                cells.Add(cx * ny + cy);
                void Visit(int xx, int yy, bool blocked)
                {
                    if (blocked) return;
                    if (xx < 0 || xx >= nx || yy < 0 || yy >= ny)
                    {
                        reachesOutside = true;
                        return;
                    }
                    if (visited[xx, yy]) return;
                    visited[xx, yy] = true;
                    queue.Enqueue((xx, yy));
                }
                Visit(cx - 1, cy, vertical[cx, cy]);
                Visit(cx + 1, cy, vertical[cx + 1, cy]);
                Visit(cx, cy - 1, horizontal[cx, cy]);
                Visit(cx, cy + 1, horizontal[cx, cy + 1]);
            }
            if (reachesOutside) continue;

            var edges = new List<Edge>();
            foreach (var cell in cells)
            {
                var cx = cell / ny;
                var cy = cell % ny;
                // Every edge is directed so the room's interior stays to its left.
                if (cy == 0 || !cells.Contains(cx * ny + cy - 1))
                    edges.Add(new Edge(cx, cy, cx + 1, cy, ys[cy] + horizontalOffset[cx, cy]));
                if (cx == nx - 1 || !cells.Contains((cx + 1) * ny + cy))
                    edges.Add(new Edge(cx + 1, cy, cx + 1, cy + 1, xs[cx + 1] - verticalOffset[cx + 1, cy]));
                if (cy == ny - 1 || !cells.Contains(cx * ny + cy + 1))
                    edges.Add(new Edge(cx + 1, cy + 1, cx, cy + 1, ys[cy + 1] - horizontalOffset[cx, cy + 1]));
                if (cx == 0 || !cells.Contains((cx - 1) * ny + cy))
                    edges.Add(new Edge(cx, cy + 1, cx, cy, xs[cx] + verticalOffset[cx, cy]));
            }

            var polygon = TraceSingleBoundary(edges, xs, ys);
            // No holes/courtyards yet: skip complex topology rather than reporting a misleading area.
            if (polygon is { Count: >= 3 } && RoomRegionGeometry.IsValid(polygon))
                rooms.Add(new RoomRegion(polygon));
        }
        return rooms;
    }

    private static List<Point2>? TraceSingleBoundary(IReadOnlyList<Edge> edges,
        IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (edges.Count < 4 || edges.Count > 2048) return null;
        var byStart = new Dictionary<(int, int), Edge>();
        foreach (var edge in edges)
            if (!byStart.TryAdd(edge.From, edge)) return null;

        var ordered = new List<Edge>();
        var seen = new HashSet<(int, int)>();
        var first = edges[0];
        var current = first;
        do
        {
            if (!seen.Add(current.From)) return null;
            ordered.Add(current);
            if (!byStart.TryGetValue(current.To, out var next)) return null;
            current = next;
        } while (current.From != first.From && ordered.Count <= edges.Count);
        if (ordered.Count != edges.Count || current.From != first.From) return null;

        var result = new List<Point2>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var previous = ordered[(i + ordered.Count - 1) % ordered.Count];
            var next = ordered[i];
            if (previous.Horizontal != next.Horizontal)
            {
                var horizontal = previous.Horizontal ? previous : next;
                var vertical = previous.Horizontal ? next : previous;
                result.Add(new Point2(vertical.Offset, horizontal.Offset));
            }
            else if (Math.Abs(previous.Offset - next.Offset) > 1e-8)
            {
                // A step in wall thickness gets a short shoulder at the shared original vertex.
                var vertex = next.From;
                if (next.Horizontal)
                {
                    result.Add(new Point2(xs[vertex.Item1], previous.Offset));
                    result.Add(new Point2(xs[vertex.Item1], next.Offset));
                }
                else
                {
                    result.Add(new Point2(previous.Offset, ys[vertex.Item2]));
                    result.Add(new Point2(next.Offset, ys[vertex.Item2]));
                }
            }
        }
        if (result.Count < 3) return null;
        if (RoomRegionGeometry.SignedArea(result) < 0) result.Reverse();
        return result;
    }
}
