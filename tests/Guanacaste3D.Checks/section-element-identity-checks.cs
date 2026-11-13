using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

/// <summary>Checks that section contours preserve individual model element ownership.</summary>
internal static class SectionElementIdentityChecks
{
    /// <summary>Intersects two walls of the same material and verifies their contours remain distinguishable.</summary>
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Section identity"));
        var level = session.Document.Levels[0].Id;
        var first = session.AddWall(level, new Point2(0, 0), new Point2(4, 0));
        var second = session.AddWall(level, new Point2(0, 4), new Point2(4, 4));
        var mesh = BuildingMesher.Build(session.Document);
        var contours = SectionGeometry.Build(mesh, new SectionCut(new Point2(2, -2), new Point2(2, 6)));
        check(contours.Any(contour => contour.ElementId == first) && contours.Any(contour => contour.ElementId == second),
            "Section contours retain the distinct identities of intersected walls with the same material");
    }
}
