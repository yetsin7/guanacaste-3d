namespace Guanacaste3D.Core;

/// <summary>Names the two ends of a wall centerline.</summary>
public enum WallEnd
{
    Start,
    End
}

/// <summary>Identifies the single other wall that meets a wall end, and the end it meets at.</summary>
public readonly record struct WallJoin(Wall Other, WallEnd OtherEnd);

/// <summary>
/// Resolves wall corners from coincident centerline endpoints on the same level. Walls on the same level join regardless of type. A corner shared by exactly two such walls is mitered so both faces meet
/// on one line. Corners where three or more walls meet, collinear continuations, and near-reversed
/// walls keep flat ends.
/// </summary>
public sealed class WallJoinIndex
{
    /// <summary>Distance in meters below which two wall endpoints count as the same corner.</summary>
    public const double Tolerance = 1e-4;

    /// <summary>Miters longer than this many half-thicknesses (very acute corners) fall back to flat ends.</summary>
    private const double MaximumMiterFactor = 4;

    private readonly Dictionary<(Guid WallId, WallEnd End), WallJoin> _joins;

    /// <summary>Stores the resolved endpoint adjacency without changing model geometry.</summary>
    private WallJoinIndex(Dictionary<(Guid WallId, WallEnd End), WallJoin> joins) => _joins = joins;

    /// <summary>Indexes every wall end that meets exactly one other wall of the same level.</summary>
    public static WallJoinIndex Build(IReadOnlyList<Wall> walls)
    {
        var joins = new Dictionary<(Guid WallId, WallEnd End), WallJoin>();
        foreach (var wall in walls)
        {
            foreach (var end in new[] { WallEnd.Start, WallEnd.End })
            {
                var corner = CornerAt(wall, end);
                var meeting = new List<WallJoin>();
                foreach (var other in walls)
                {
                    if (other.Id == wall.Id || other.LevelId != wall.LevelId) continue;
                    if (corner.DistanceTo(other.Start) <= Tolerance) meeting.Add(new WallJoin(other, WallEnd.Start));
                    if (corner.DistanceTo(other.End) <= Tolerance) meeting.Add(new WallJoin(other, WallEnd.End));
                }
                if (meeting.Count == 1) joins[(wall.Id, end)] = meeting[0];
            }
        }
        return new WallJoinIndex(joins);
    }

    /// <summary>Returns the plan footprint of a wall with each joined end mitered to its shared corner.</summary>
    public WallFootprint Footprint(Wall wall)
    {
        var start = EndFaces(wall, WallEnd.Start);
        var end = EndFaces(wall, WallEnd.End);
        return new WallFootprint(start.Left, end.Left, end.Right, start.Right);
    }

    /// <summary>
    /// Returns the plan outline of the section between two stations along a wall's centerline.
    /// Only a section that starts or ends at a wall end takes that end's miter.
    /// </summary>
    public IReadOnlyList<Point2> SectionOutline(Wall wall, double from, double to)
    {
        var footprint = Footprint(wall);
        var (directionX, directionY) = Direction(wall);
        var (normalX, normalY) = LeftNormal(wall);
        var half = wall.Thickness / 2;

        /// <summary>Returns a square-section face point away from joined ends.</summary>
        Point2 Station(double distance, double side) => new(
            wall.Start.X + directionX * distance + side * normalX * half,
            wall.Start.Y + directionY * distance + side * normalY * half);

        var startSquare = from <= Tolerance;
        var endSquare = to >= wall.Length - Tolerance;
        return
        [
            startSquare ? footprint.StartLeft : Station(from, 1),
            endSquare ? footprint.EndLeft : Station(to, 1),
            endSquare ? footprint.EndRight : Station(to, -1),
            startSquare ? footprint.StartRight : Station(from, -1)
        ];
    }

    /// <summary>
    /// Returns the left and right face points at one wall end. A joined end meets its neighbour's
    /// face at the intersection of both faces; any other end is square to the wall.
    /// </summary>
    private (Point2 Left, Point2 Right) EndFaces(Wall wall, WallEnd end)
    {
        var corner = CornerAt(wall, end);
        var (normalX, normalY) = LeftNormal(wall);
        var half = wall.Thickness / 2;
        if (!_joins.TryGetValue((wall.Id, end), out var join))
            return Square(corner, normalX * half, normalY * half);

        var (otherNormalX, otherNormalY) = LeftNormal(join.Other);
        // Equal endpoint roles reverse the neighbour's face correspondence.
        if (end == join.OtherEnd)
        {
            otherNormalX = -otherNormalX;
            otherNormalY = -otherNormalY;
        }
        var otherHalf = join.Other.Thickness / 2;
        var determinant = normalX * otherNormalY - normalY * otherNormalX;
        if (Math.Abs(determinant) < 1e-9)
            return Square(corner, normalX * half, normalY * half);

        // Solves n·x = half for both walls: x is the left-face corner relative to the shared corner.
        var offsetX = (half * otherNormalY - otherHalf * normalY) / determinant;
        var offsetY = (normalX * otherHalf - otherNormalX * half) / determinant;
        if (Math.Sqrt(offsetX * offsetX + offsetY * offsetY) > Math.Min(MaximumMiterFactor * Math.Max(half, otherHalf),
                Math.Min(wall.Length, join.Other.Length) / 2))
            return Square(corner, normalX * half, normalY * half);
        return (new Point2(corner.X + offsetX, corner.Y + offsetY),
            new Point2(corner.X - offsetX, corner.Y - offsetY));
    }

    /// <summary>Returns the face points of a square end, offset from the corner by the given vector.</summary>
    private static (Point2 Left, Point2 Right) Square(Point2 corner, double offsetX, double offsetY) =>
        (new Point2(corner.X + offsetX, corner.Y + offsetY), new Point2(corner.X - offsetX, corner.Y - offsetY));

    /// <summary>Returns the centerline point at one end of a wall.</summary>
    private static Point2 CornerAt(Wall wall, WallEnd end) => end == WallEnd.Start ? wall.Start : wall.End;

    /// <summary>Returns the unit vector from the start of a wall toward its end.</summary>
    private static (double X, double Y) Direction(Wall wall) =>
        ((wall.End.X - wall.Start.X) / wall.Length, (wall.End.Y - wall.Start.Y) / wall.Length);

    /// <summary>Returns the unit normal on the left of the start-to-end direction.</summary>
    private static (double X, double Y) LeftNormal(Wall wall)
    {
        var (directionX, directionY) = Direction(wall);
        return (-directionY, directionX);
    }
}
