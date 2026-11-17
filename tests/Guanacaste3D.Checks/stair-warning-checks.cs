using Guanacaste3D.Core;

/// <summary>The stair rules of Review warnings: comfortable going and the opening in the slab above.</summary>
static class StairWarningChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Stairs"));
        var ground = session.Document.Levels[0].Id;
        var upper = session.Document.Levels.OrderBy(level => level.Elevation).Skip(1).First().Id;
        var upperElevation = session.Document.Levels.Single(level => level.Id == upper).Elevation;
        session.AddFloor(upper, [new(-1, -1), new(7, -1), new(7, 7), new(-1, 7)], 0.2);
        var preset = ArchitecturalLibrary.PresetsFor(StructuralElementKind.Stair).First() with { Height = upperElevation };

        var steep = session.AddElementFromPreset(ground, preset, new(1, 1), new(1, 5));
        var codes = ModelWarnings.Analyze(session.Document).Where(warning => warning.ElementIds.Contains(steep)).Select(warning => warning.Code).ToList();
        check(codes.Contains("StairTreadTooShallow"), "Stair warnings: a 4 m run for a 3 m storey has short treads and is reported in Review warnings");
        check(codes.Contains("StairNoOpening"), "Stair warnings: a stair arriving under a solid slab is reported");

        session.AddOpening(upper, StructuralElementKind.Shaft, "Hueco", new(0.4, 1), new(1.6, 5));
        codes = ModelWarnings.Analyze(session.Document).Where(warning => warning.ElementIds.Contains(steep)).Select(warning => warning.Code).ToList();
        check(!codes.Contains("StairNoOpening"), "Stair warnings: the shaft cut over the stair clears the missing-opening warning");

        var gentle = session.AddElementFromPreset(ground, preset, new(4, 0), new(4, 5.4));
        check(!ModelWarnings.Analyze(session.Document).Any(warning => warning.ElementIds.Contains(gentle) && warning.Code == "StairTreadTooShallow"),
            "Stair warnings: a 5.4 m run for the same storey has comfortable treads");
    }
}
