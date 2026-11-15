using Guanacaste3D.Core;

namespace HouseFlowChecks;

/// <summary>One action of the house build, written the way the Revit script reads it.</summary>
/// <param name="Number">Row in the MEJORAS.md flow table.</param>
/// <param name="Revit">What is done in Revit, in the same words as the table.</param>
/// <param name="Do">The same action in Guanacaste 3D, through the ribbon and the plan.</param>
/// <param name="Expect">What the document must show afterwards; returns an error text, or null when it holds.</param>
internal sealed record HouseStep(int Number, string Revit, Action<HouseDriver> Do, Func<HouseDriver, string?> Expect);

internal static class HouseSteps
{
    private static readonly Point2[] Corners =
        [new(0, 0), new(8, 0), new(8, 6), new(0, 6)];

    /// <summary>Steps 1, 2, 5 and 8 of the flow table; later steps are added as their buttons are audited.</summary>
    public static IReadOnlyList<HouseStep> All { get; } =
    [
        new(1, "Proyecto nuevo con niveles",
            _ => { },
            d => d.Session.Document.Levels.Count >= 1 ? null : "El proyecto nuevo no trae ningun nivel"),

        new(2, "Segundo nivel (Arquitectura > Nivel)",
            d =>
            {
                d.Marks["levels"] = d.Session.Document.Levels.Count;
                d.PressRibbon("Level");
            },
            d => d.Session.Document.Levels.Count > d.Marks["levels"]
                ? null
                : $"Pulsar Nivel no creo ningun nivel (antes {d.Marks["levels"]}, ahora {d.Session.Document.Levels.Count}); estado: {d.Status}"),

        new(5, "Muro (Arquitectura > Construir > Muro): contorno cerrado de 8 x 6 m",
            d =>
            {
                d.ZoomPlan(60);
                d.PressRibbon("Wall");
                d.SetCheck("WallChainCheck", true);
                d.SetCheck("WallOrthogonalCheck", true);
                foreach (var corner in Corners) d.ClickWall(corner);
                d.ClickWall(Corners[0]);
            },
            d =>
            {
                var walls = d.Session.Document.Walls;
                if (walls.Count != 4) return $"Se esperaban 4 muros y hay {walls.Count}";
                var closed = walls[^1].End.DistanceTo(walls[0].Start) < 1e-6;
                return closed ? null : "El ultimo muro no cierra sobre el primero";
            }),

        new(8, "Puerta (Arquitectura > Construir > Puerta)",
            d => d.PressRibbon("Door"),
            d => d.PlacingOpening ? null : $"Puerta no dejo una puerta lista para colocar; estado: {d.Status}")
    ];

    /// <summary>
    /// TODO(human): decide what "a door was placed correctly" means for step 8 and return an error text, or null when it
    /// holds. Today step 8 only checks that the tool is armed.
    /// </summary>
    public static string? DoorPlacedCorrectly(HouseDriver driver)
    {
        return null;
    }
}
