using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;

internal static partial class InteractionChecks
{
    /// <summary>Exercises floor and terrain duplication through the ribbon, preserving geometry and history.</summary>
    private static void DuplicateFloorRibbonChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        Point2[] boundary = [new(410, 410), new(414, 410), new(414, 414), new(410, 414)];
        var source = session.AddFloor(level, boundary, 0.4, "Test terrain", FloorKind.Toposolid);
        session.AddTopoPoint(source, new TopoPoint(412, 412, 0.8));
        Guid? copy = null;
        try
        {
            SetField("_selectedWallId", null);
            SetField("_selectedColumnId", null);
            SetField("_selectedStructuralId", null);
            SetField("_selectedFloorId", (Guid?)source);
            Invoke("ExecuteApplicationCommand", "CopyElement");
            copy = Field<Guid?>("_selectedFloorId");
            var original = session.Document.Floors.Single(floor => floor.Id == source);
            var duplicate = session.Document.Floors.Single(floor => floor.Id == copy);
            Check(copy != source && duplicate.Kind == original.Kind && duplicate.Thickness == original.Thickness &&
                duplicate.Boundary.SequenceEqual(original.Boundary.Select(point => new Point2(point.X + 1, point.Y + 1))) &&
                duplicate.TopoPoints!.SequenceEqual(original.TopoPoints!.Select(point =>
                    point with { X = point.X + 1, Y = point.Y + 1 })),
                "Copy Element duplicates terrain with its translated boundary and surveyed elevations");
            session.Undo();
            Check(session.Document.Floors.All(floor => floor.Id != copy), "Undo removes the terrain copy");
            session.Redo();
            Check(session.Document.Floors.Any(floor => floor.Id == copy), "Redo restores the terrain copy");
        }
        finally
        {
            SetField("_selectedFloorId", null);
            if (copy is Guid copyId) session.RemoveFloor(copyId);
            session.RemoveFloor(source);
        }
    }
}
