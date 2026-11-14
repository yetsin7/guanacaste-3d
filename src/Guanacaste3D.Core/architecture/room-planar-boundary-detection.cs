namespace Guanacaste3D.Core;

/// <summary>
/// Extracts bounded faces from a planar wall/separator graph. Works with walls at arbitrary
/// angles and with nested, disconnected boundary loops (courtyards); the legacy orthogonal
/// detector is not involved. A half-edge face walk avoids filling the missing center of a patio.
/// </summary>
public static class RoomPlanarBoundaryDetection
{
    private const double Tolerance = 0.00001;
    private const int MaximumSegments = 1024;
    private sealed record Segment(Point2 Start, Point2 End, double HalfThickness);
    private sealed record Edge(int From, int To, double HalfThickness);
    private sealed record Loop(List<Point2> Raw, List<Edge> Edges, double SignedArea);

    /// <summary>Discovers all bounded room faces; nested inner loops are labeled as courtyard candidates.</summary>
    public static IReadOnlyList<RoomRegion> Detect(BuildingDocument document, Guid levelId)
    {
        var segments = document.Walls.Where(w => w.LevelId == levelId)
            .Select(w => new Segment(w.Start, w.End, w.Thickness / 2))
            .Concat(document.StructuralElements.Where(element =>
                element.LevelId == levelId && element.Kind == StructuralElementKind.RoomSeparator)
                .Select(element => new Segment(element.Start, element.End, 0)))
            .Concat(document.CurvedRoomSeparators.Where(arc => arc.LevelId == levelId)
                .SelectMany(arc =>
                {
                    var samples = arc.SamplePoints();
                    return Enumerable.Range(0, samples.Count - 1)
                        .Select(i => new Segment(samples[i], samples[i + 1], 0));
                }))
            // Curtain walls bound rooms, as in the reference application: a glazed living room is still a room.
            .Concat(document.StructuralElements.Where(element =>
                    element.LevelId == levelId && element.Kind == StructuralElementKind.CurtainWall)
                .Select(element => new Segment(element.Start, element.End, element.Width / 2)))
            .Where(segment => segment.Start.DistanceTo(segment.End) > Tolerance)
            .ToList();
        segments = ExtendToFaces(segments);
        if (segments.Count < 3) return [];
        if (segments.Count > MaximumSegments)
            throw new InvalidDataException("The room boundary graph is too complex.");

        var split = segments.Select(segment => new List<Point2> { segment.Start, segment.End }).ToArray();
        for (var i = 0; i < segments.Count; i++)
        for (var j = i + 1; j < segments.Count; j++)
            SplitIntersections(segments[i], segments[j], split[i], split[j]);

        var vertices = new List<Point2>();
        var lookup = new Dictionary<(long X, long Y), int>();
        int Vertex(Point2 point)
        {
            var key = ((long)Math.Round(point.X / Tolerance), (long)Math.Round(point.Y / Tolerance));
            if (lookup.TryGetValue(key, out var existing)) return existing;
            var index = vertices.Count;
            lookup.Add(key, index);
            vertices.Add(point);
            return index;
        }

        var edgeThickness = new Dictionary<(int A, int B), double>();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            var denominator = dx * dx + dy * dy;
            var ordered = split[i].OrderBy(point =>
                ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / denominator)
                .Select(Vertex).ToArray();
            for (var index = 0; index < ordered.Length - 1; index++)
            {
                var a = ordered[index];
                var b = ordered[index + 1];
                if (a == b || vertices[a].DistanceTo(vertices[b]) <= Tolerance) continue;
                var key = a < b ? (a, b) : (b, a);
                edgeThickness[key] = Math.Max(edgeThickness.GetValueOrDefault(key), segment.HalfThickness);
            }
        }
        if (edgeThickness.Count > 4096) throw new InvalidDataException("The room boundary graph is too complex.");

        var edges = new List<Edge>();
        var outgoing = new Dictionary<int, List<int>>();
        foreach (var (key, half) in edgeThickness)
        {
            var first = edges.Count;
            edges.Add(new Edge(key.A, key.B, half));
            edges.Add(new Edge(key.B, key.A, half));
            if (!outgoing.TryGetValue(key.A, out var atA)) outgoing[key.A] = atA = [];
            if (!outgoing.TryGetValue(key.B, out var atB)) outgoing[key.B] = atB = [];
            atA.Add(first);
            atB.Add(first + 1);
        }
        foreach (var list in outgoing.Values)
            list.Sort((a, b) =>
            {
                double Angle(int edgeIndex)
                {
                    var edge = edges[edgeIndex];
                    var start = vertices[edge.From];
                    var end = vertices[edge.To];
                    return Math.Atan2(end.Y - start.Y, end.X - start.X);
                }
                return Angle(a).CompareTo(Angle(b));
            });

        var walked = new bool[edges.Count];
        var loops = new List<Loop>();
        for (var initial = 0; initial < edges.Count; initial++)
        {
            if (walked[initial]) continue;
            var directed = new List<Edge>();
            var current = initial;
            var complete = false;
            for (var step = 0; step <= edges.Count; step++)
            {
                if (walked[current])
                {
                    complete = current == initial;
                    break;
                }
                walked[current] = true;
                var edge = edges[current];
                directed.Add(edge);
                var neighbors = outgoing[edge.To];
                var twin = current ^ 1;
                var twinIndex = neighbors.IndexOf(twin);
                if (twinIndex < 0) break;
                current = neighbors[(twinIndex - 1 + neighbors.Count) % neighbors.Count];
            }
            if (!complete || directed.Count < 3) continue;
            var raw = directed.Select(edge => vertices[edge.From]).ToList();
            var area = RoomRegionGeometry.SignedArea(raw);
            if (Math.Abs(area) < 0.1 || !RoomRegionGeometry.IsValid(raw)) continue;
            loops.Add(new Loop(raw, directed, area));
        }

        var positives = loops.Where(loop => loop.SignedArea > 0).ToArray();
        var negatives = loops.Where(loop => loop.SignedArea < 0).ToArray();
        var result = new List<RoomRegion>();
        foreach (var outer in positives)
        {
            var isNested = positives.Any(other =>
                other.SignedArea > outer.SignedArea + 1e-5 &&
                RoomRegionGeometry.Contains(other.Raw, outer.Raw[0]));

            var inner = negatives.Where(hole =>
            {
                if (Math.Abs(hole.SignedArea) >= outer.SignedArea - 1e-5 ||
                    !RoomRegionGeometry.Contains(outer.Raw, hole.Raw[0]))
                    return false;
                // Assign each negative loop only to its nearest enclosing positive boundary.
                return !positives.Any(parent =>
                    parent != outer && parent.SignedArea > Math.Abs(hole.SignedArea) + 1e-5 &&
                    parent.SignedArea < outer.SignedArea - 1e-5 &&
                    RoomRegionGeometry.Contains(parent.Raw, hole.Raw[0]));
            }).ToArray();

            var boundary = OffsetFaceLoop(outer, vertices);
            if (boundary is null) continue;
            var holes = new List<IReadOnlyList<Point2>>();
            var valid = true;
            foreach (var hole in inner)
            {
                var ring = OffsetFaceLoop(hole, vertices);
                if (ring is null) { valid = false; break; }
                holes.Add(ring);
            }
            if (!valid || !RoomRegionGeometry.ValidRegion(boundary, holes)) continue;
            result.Add(new RoomRegion(boundary)
            {
                Holes = holes,
                IsCourtyard = isNested
            });
        }
        return result.OrderBy(region => region.Min.X).ThenBy(region => region.Min.Y).ToArray();
    }

    /// <summary>Extra reach beyond the half thickness when a wall end is taken to stop at another wall's face.</summary>
    private const double FaceReach = 0.03;

    /// <summary>
    /// A wall drawn to the face of another (a partition stopping at 6.70 against a 30 cm wall on 6.55) ends short of that
    /// wall's centerline, so the boundary graph would leave it loose and the room would not close. Each loose end is
    /// carried forward along its own wall until it meets the centerline in front of it, when that centerline is within
    /// the other wall's half thickness. Joined corners and walls that already cross are left as they are.
    /// </summary>
    private static List<Segment> ExtendToFaces(List<Segment> segments)
    {
        bool Loose(Point2 end, int self)
        {
            for (var index = 0; index < segments.Count; index++)
                if (index != self && Distance(end, segments[index]) <= 1e-4) return false;
            return true;
        }

        Point2 Extend(Point2 end, Point2 opposite, int self)
        {
            if (!Loose(end, self)) return end;
            var length = end.DistanceTo(opposite);
            if (length <= Tolerance) return end;
            var (dx, dy) = ((end.X - opposite.X) / length, (end.Y - opposite.Y) / length);
            var best = double.MaxValue;
            for (var index = 0; index < segments.Count; index++)
            {
                if (index == self) continue;
                var other = segments[index];
                var (ex, ey) = (other.End.X - other.Start.X, other.End.Y - other.Start.Y);
                var denominator = dx * ey - dy * ex;
                if (Math.Abs(denominator) < 1e-9) continue;
                var (wx, wy) = (other.Start.X - end.X, other.Start.Y - end.Y);
                var t = (wx * ey - wy * ex) / denominator;
                var u = (wx * dy - wy * dx) / denominator;
                if (t < -1e-9 || t > other.HalfThickness + FaceReach || u < -1e-6 || u > 1 + 1e-6) continue;
                best = Math.Min(best, t);
            }
            return best == double.MaxValue ? end : new Point2(end.X + dx * best, end.Y + dy * best);
        }

        return segments.Select((segment, index) => segment with
        {
            Start = Extend(segment.Start, segment.End, index),
            End = Extend(segment.End, segment.Start, index)
        }).ToList();
    }

    private static double Distance(Point2 point, Segment segment)
    {
        var (dx, dy) = (segment.End.X - segment.Start.X, segment.End.Y - segment.Start.Y);
        var squared = dx * dx + dy * dy;
        var t = squared < 1e-12 ? 0 : Math.Clamp(((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / squared, 0, 1);
        return point.DistanceTo(new Point2(segment.Start.X + dx * t, segment.Start.Y + dy * t));
    }

    private static void SplitIntersections(Segment a, Segment b, List<Point2> onA, List<Point2> onB)
    {
        var ax = a.End.X - a.Start.X;
        var ay = a.End.Y - a.Start.Y;
        var bx = b.End.X - b.Start.X;
        var by = b.End.Y - b.Start.Y;
        var cross = ax * by - ay * bx;
        if (Math.Abs(cross) > 1e-12)
        {
            var dx = b.Start.X - a.Start.X;
            var dy = b.Start.Y - a.Start.Y;
            var ta = (dx * by - dy * bx) / cross;
            var tb = (dx * ay - dy * ax) / cross;
            if (ta < -Tolerance || ta > 1 + Tolerance ||
                tb < -Tolerance || tb > 1 + Tolerance) return;
            var point = new Point2(a.Start.X + Math.Clamp(ta, 0, 1) * ax,
                a.Start.Y + Math.Clamp(ta, 0, 1) * ay);
            onA.Add(point);
            onB.Add(point);
            return;
        }

        bool On(Point2 point, Segment segment)
        {
            var vx = segment.End.X - segment.Start.X;
            var vy = segment.End.Y - segment.Start.Y;
            var px = point.X - segment.Start.X;
            var py = point.Y - segment.Start.Y;
            if (Math.Abs(vx * py - vy * px) > Tolerance * double.Hypot(vx, vy)) return false;
            var t = (px * vx + py * vy) / (vx * vx + vy * vy);
            return t >= -Tolerance && t <= 1 + Tolerance;
        }
        foreach (var p in new[] { a.Start, a.End })
            if (On(p, b)) onB.Add(p);
        foreach (var p in new[] { b.Start, b.End })
            if (On(p, a)) onA.Add(p);
    }

    private static List<Point2>? OffsetFaceLoop(Loop loop, IReadOnlyList<Point2> vertices)
    {
        var result = new List<Point2>();
        var parts = loop.Edges;
        for (var i = 0; i < parts.Count; i++)
        {
            var previous = parts[(i + parts.Count - 1) % parts.Count];
            var current = parts[i];
            var vertex = vertices[current.From];
            var prevStart = vertices[previous.From];
            var prevEnd = vertices[previous.To];
            var currEnd = vertices[current.To];
            var a = new Point2(prevEnd.X - prevStart.X, prevEnd.Y - prevStart.Y);
            var b = new Point2(currEnd.X - vertex.X, currEnd.Y - vertex.Y);
            var aLength = double.Hypot(a.X, a.Y);
            var bLength = double.Hypot(b.X, b.Y);
            if (aLength < Tolerance || bLength < Tolerance) return null;
            var ap = new Point2(vertex.X - a.Y / aLength * previous.HalfThickness,
                vertex.Y + a.X / aLength * previous.HalfThickness);
            var bp = new Point2(vertex.X - b.Y / bLength * current.HalfThickness,
                vertex.Y + b.X / bLength * current.HalfThickness);
            var det = a.X * b.Y - a.Y * b.X;
            if (Math.Abs(det) <= 1e-10)
            {
                result.Add(ap);
                if (ap.DistanceTo(bp) > Tolerance) result.Add(bp);
                continue;
            }
            var dx = bp.X - ap.X;
            var dy = bp.Y - ap.Y;
            var t = (dx * b.Y - dy * b.X) / det;
            var intersection = new Point2(ap.X + a.X * t, ap.Y + a.Y * t);
            if (intersection.DistanceTo(vertex) > 5) return null;
            result.Add(intersection);
        }
        // Remove duplicate neighbors, without changing the usable footprint.
        for (var i = result.Count - 1; i >= 0 && result.Count > 3; i--)
            if (result[i].DistanceTo(result[(i + 1) % result.Count]) < Tolerance)
                result.RemoveAt(i);
        return result.Count >= 3 && RoomRegionGeometry.IsValid(result) ? result : null;
    }
}
