namespace Guanacaste3D.Core;

/// <summary>Identifies the wall plane that follows the user's drawing reference.</summary>
public enum WallLocationReference
{
    Centerline,
    ExteriorFace,
    InteriorFace
}

/// <summary>Represents the model centerline generated from a wall drawing reference.</summary>
public readonly record struct WallCenterline(Point2 Start, Point2 End);

/// <summary>Builds precise wall centerlines from contextual placement options.</summary>
public static class WallPlacementGeometry
{
    /// <summary>Locks a candidate endpoint to the dominant axis when orthogonal drawing is active.</summary>
    public static Point2 ConstrainEndpoint(Point2 start, Point2 candidate, bool orthogonal)
    {
        if (!orthogonal) return candidate;
        return Math.Abs(candidate.X - start.X) >= Math.Abs(candidate.Y - start.Y)
            ? new Point2(candidate.X, start.Y)
            : new Point2(start.X, candidate.Y);
    }

    /// <summary>Offsets a drawn reference line to the centerline required by the selected wall plane.</summary>
    public static WallCenterline OffsetLine(
        Point2 start,
        Point2 end,
        double thickness,
        double offset,
        WallLocationReference locationReference)
    {
        var length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        if (length < 0.05) throw new InvalidDataException("A wall is invalid or has no associated level.");
        var distance = offset + CenterlineShift(thickness, locationReference);
        var normalX = -(end.Y - start.Y) / length;
        var normalY = (end.X - start.X) / length;
        return new WallCenterline(
            new Point2(start.X + normalX * distance, start.Y + normalY * distance),
            new Point2(end.X + normalX * distance, end.Y + normalY * distance));
    }

    /// <summary>Creates four connected centerlines from opposite corners as one rectangular wall loop.</summary>
    public static IReadOnlyList<WallCenterline> Rectangle(
        Point2 first,
        Point2 opposite,
        double thickness,
        double offset,
        WallLocationReference locationReference)
    {
        var inset = offset + CenterlineShift(thickness, locationReference);
        var minimumX = Math.Min(first.X, opposite.X) + inset;
        var maximumX = Math.Max(first.X, opposite.X) - inset;
        var minimumY = Math.Min(first.Y, opposite.Y) + inset;
        var maximumY = Math.Max(first.Y, opposite.Y) - inset;
        if (maximumX - minimumX < 0.05 || maximumY - minimumY < 0.05)
            throw new InvalidDataException("A wall is invalid or has no associated level.");
        var corners = new[]
        {
            new Point2(minimumX, minimumY), new Point2(maximumX, minimumY),
            new Point2(maximumX, maximumY), new Point2(minimumX, maximumY)
        };
        return Enumerable.Range(0, corners.Length)
            .Select(index => new WallCenterline(corners[index], corners[(index + 1) % corners.Length]))
            .ToArray();
    }

    /// <summary>Converts a face reference into the signed distance from that face to the wall centerline.</summary>
    private static double CenterlineShift(double thickness, WallLocationReference locationReference) =>
        locationReference switch
        {
            WallLocationReference.ExteriorFace => thickness / 2,
            WallLocationReference.InteriorFace => -thickness / 2,
            _ => 0
        };
}
