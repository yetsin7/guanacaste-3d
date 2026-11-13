using Guanacaste3D.Core;

/// <summary>Checks that roof-hosted elements respond only to changes of their roof.</summary>
static class RoofHostedElementChecks
{
    /// <summary>Exercises non-roof slab edits, roof edits, and their undo history on a shared footprint.</summary>
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Roof hosts"));
        var level = session.Document.Levels[0].Id;
        Point2[] boundary = [new(0, 0), new(8, 0), new(8, 6), new(0, 6)];
        var roof = session.AddFloor(level, boundary, 0.2, kind: FloorKind.Roof, elevationOffset: 3);
        session.SetRoofShape(roof, RoofShape.Gable, 25);
        var dormer = session.AddDormer(level, "Dormer", new(1, 1), new(2, 2));
        var skylight = session.AddSkylight(level, "Skylight", new(5, 1), new(6, 2));
        var original = session.Document.StructuralElements.Where(item => item.Id == dormer || item.Id == skylight)
            .ToDictionary(item => item.Id, item => item.Offset);
        check(original.Count == 2, "The test roof hosts both a dormer and a skylight");
        foreach (var kind in new[] { FloorKind.Floor, FloorKind.Ceiling, FloorKind.Toposolid })
        {
            var slab = session.AddFloor(level, boundary, 0.2, kind: kind);
            session.UpdateFloor(slab, "Updated slab", 0.3, 0.5);
            check(session.Document.StructuralElements.Where(item => original.ContainsKey(item.Id))
                    .All(item => Math.Abs(item.Offset - original[item.Id]) < 1e-9),
                $"Editing {kind} does not move roof-hosted elements");
        }
        session.UpdateFloor(roof, "Raised roof", 0.2, 4);
        check(session.Document.StructuralElements.Where(item => original.ContainsKey(item.Id))
                .All(item => Math.Abs(item.Offset - original[item.Id] - 1) < 1e-9),
            "Raising the roof updates both hosted elements");
        session.Undo();
        check(session.Document.StructuralElements.Where(item => original.ContainsKey(item.Id))
                .All(item => Math.Abs(item.Offset - original[item.Id]) < 1e-9),
            "Undo restores the roof and hosted element elevations together");
        CheckOverlappingRoofs(check);
    }
    /// <summary>Verifies that overlapping roofs do not both claim or cut the same roof-hosted element.</summary>
    private static void CheckOverlappingRoofs(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Overlapping roofs"));
        var level = session.Document.Levels[0].Id;
        Point2[] boundary = [new(0, 0), new(8, 0), new(8, 6), new(0, 6)];
        var host = session.AddFloor(level, boundary, 0.2, kind: FloorKind.Roof, elevationOffset: 3);
        var other = session.AddFloor(level, boundary, 0.2, kind: FloorKind.Roof, elevationOffset: 6);
        var skylight = session.AddSkylight(level, "Skylight", new(1, 1), new(2, 2));
        session.UpdateFloor(other, "Other roof", 0.2, 7);
        check(session.Document.StructuralElements.Single(item => item.Id == skylight).Offset == 3,
            "Editing another overlapping roof does not move the skylight");
        var mesh = Guanacaste3D.Core.Geometry.BuildingMesher.Build(session.Document);
        /// <summary>Measures the horizontal top triangles attributed to one roof.</summary>
        double TopArea(Guid roofId, float elevation)
        {
            var range = mesh.ElementRanges.Single(item => item.ElementId == roofId);
            double area = 0;
            for (var triangle = range.First; triangle < range.End; triangle++)
            {
                var a = mesh.Positions[mesh.Indices[triangle * 3]];
                var b = mesh.Positions[mesh.Indices[triangle * 3 + 1]];
                var c = mesh.Positions[mesh.Indices[triangle * 3 + 2]];
                if (Math.Abs(a.Z - elevation) > 1e-5 || Math.Abs(b.Z - elevation) > 1e-5 || Math.Abs(c.Z - elevation) > 1e-5) continue;
                area += System.Numerics.Vector3.Cross(b - a, c - a).Length() / 2;
            }
            return area;
        }
        check(Math.Abs(TopArea(host, 3) - 47) < 1e-4 && Math.Abs(TopArea(other, 7) - 48) < 1e-4,
            "Only the roof hosting the skylight is cut in the model mesh");
    }

}
