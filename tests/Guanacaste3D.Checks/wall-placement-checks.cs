using Guanacaste3D.Core;

/// <summary>Regression checks for contextual line and rectangle wall placement geometry.</summary>
static class WallPlacementChecks
{
    /// <summary>Verifies orthogonal constraints, face references, offsets, and connected rectangle loops.</summary>
    public static void Run(Action<bool, string> check)
    {
        var horizontal = WallPlacementGeometry.ConstrainEndpoint(
            new Point2(0, 0), new Point2(4, 1), orthogonal: true);
        var vertical = WallPlacementGeometry.ConstrainEndpoint(
            new Point2(0, 0), new Point2(1, 4), orthogonal: true);
        check(horizontal == new Point2(4, 0) && vertical == new Point2(0, 4),
            "Orthogonal wall placement follows the dominant drawing axis");

        var exterior = WallPlacementGeometry.OffsetLine(
            new Point2(0, 0), new Point2(5, 0), 0.20, 0.10, WallLocationReference.ExteriorFace);
        check(exterior.Start == new Point2(0, 0.20) && exterior.End == new Point2(5, 0.20),
            "Exterior-face placement combines half-thickness and explicit offset");

        var rectangle = WallPlacementGeometry.Rectangle(
            new Point2(0, 0), new Point2(6, 4), 0.20, 0, WallLocationReference.ExteriorFace);
        var connected = rectangle.Count == 4 && Enumerable.Range(0, rectangle.Count)
            .All(index => rectangle[index].End == rectangle[(index + 1) % rectangle.Count].Start);
        check(connected && rectangle[0].Start == new Point2(0.10, 0.10) &&
              rectangle[1].End == new Point2(5.90, 3.90),
            "Rectangle placement produces four joined inset wall centerlines");

        var rejected = false;
        try
        {
            WallPlacementGeometry.Rectangle(
                new Point2(0, 0), new Point2(0.15, 2), 0.20, 0, WallLocationReference.ExteriorFace);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        check(rejected, "Face offsets cannot collapse a rectangular wall loop");
    }
}
