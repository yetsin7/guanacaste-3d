namespace Guanacaste3D.Core.Interop;

/// <summary>
/// The printed plan of a discipline view: the architectural base drawn halftone, with the discipline's own work drawn on
/// top at full strength — devices and circuits for electrical, pipes and points for plumbing, columns and foundations
/// for structure, ducts, diffusers and air conditioners for mechanical. Architectural plans print as before; coordination
/// plans print every discipline at full strength.
/// </summary>
public static partial class PdfPlanExporter
{
    /// <summary>How far a halftone colour is blended toward white.</summary>
    private const double HalftoneBlend = 0.7;

    private static readonly (double, double, double) ElectricalInk = (0.75, 0.1, 0.1);
    private static readonly (double, double, double) MechanicalInk = (0.1, 0.45, 0.25);

    public static IReadOnlyList<SheetPrimitive> PlanPrimitives(BuildingDocument document, Guid levelId, ViewDiscipline discipline)
    {
        ArgumentNullException.ThrowIfNull(document);
        var basePlan = PlanPrimitives(document, levelId);
        if (discipline == ViewDiscipline.Architectural) return basePlan;
        var items = new List<SheetPrimitive>(discipline == ViewDiscipline.Coordination ? basePlan : basePlan.Select(Halftone));
        foreach (var own in Enum.GetValues<ViewDiscipline>())
            if (own == discipline || discipline == ViewDiscipline.Coordination)
                items.AddRange(own switch
                {
                    ViewDiscipline.Structural => StructuralPrimitives(document, levelId),
                    ViewDiscipline.Electrical => ElectricalPrimitives(document, levelId),
                    ViewDiscipline.Plumbing => PlumbingPrimitives(document, levelId),
                    ViewDiscipline.Mechanical => MechanicalPrimitives(document, levelId),
                    _ => []
                });
        return items;
    }

    /// <summary>The same primitive, lighter and thinner, as the reference application draws other disciplines.</summary>
    public static SheetPrimitive Halftone(SheetPrimitive primitive)
    {
        static (double, double, double) Light((double R, double G, double B) color) =>
            (color.R + (1 - color.R) * HalftoneBlend, color.G + (1 - color.G) * HalftoneBlend, color.B + (1 - color.B) * HalftoneBlend);
        return primitive switch
        {
            SheetPolygon polygon => polygon with { Color = Light(polygon.Color), Fill = polygon.Fill is { } fill ? Light(fill) : null, Weight = polygon.Weight * 0.7 },
            SheetPolyline line => line with { Color = Light(line.Color), Weight = line.Weight * 0.7 },
            SheetText text => text with { Color = Light(text.Color) },
            _ => primitive
        };
    }

    private static IEnumerable<SheetPrimitive> StructuralPrimitives(BuildingDocument document, Guid levelId)
    {
        foreach (var column in document.Columns.Where(column => column.LevelId == levelId))
            yield return new SheetPolygon(column.Outline, 0.5, Black, (0.15, 0.15, 0.15));
        foreach (var element in document.StructuralElements.Where(element => element.LevelId == levelId &&
                     ViewDisciplines.Of(element) == ViewDiscipline.Structural))
        {
            var foundation = element.Kind is StructuralElementKind.WallFoundation or StructuralElementKind.IsolatedFoundation;
            yield return new SheetPolygon(element.Footprint, 0.35, Black, null);
            if (foundation) yield return new SheetText(Centre(element.Footprint), element.Kind == StructuralElementKind.IsolatedFoundation ? "Z" : "CC", 2.2, Black, true);
        }
    }

    private static IEnumerable<SheetPrimitive> ElectricalPrimitives(BuildingDocument document, Guid levelId)
    {
        var devices = document.ElectricalDevices.Where(device => device.LevelId == levelId).ToList();
        var byId = document.ElectricalDevices.ToDictionary(device => device.Id);
        // Home runs: every device on a circuit is joined to its panel by a dashed line, as on a power plan.
        foreach (var device in devices.Where(device => device.CircuitId is not null))
        {
            var circuit = document.ElectricalCircuits.FirstOrDefault(candidate => candidate.Id == device.CircuitId);
            if (circuit is null || !byId.TryGetValue(circuit.PanelId, out var panel) || panel.LevelId != levelId) continue;
            yield return new SheetPolyline([device.Position, panel.Position], 0.18, ElectricalInk, true);
        }
        foreach (var device in devices)
        foreach (var primitive in ElectricalSymbol(device))
            yield return primitive;
    }

    /// <summary>A plan symbol per device: receptacle, switch, luminaire, panel or air conditioner.</summary>
    private static IEnumerable<SheetPrimitive> ElectricalSymbol(ElectricalDevice device)
    {
        var at = device.Position;
        if (device.Kind == ElectricalDeviceKind.MiniSplit) yield break;
        if (device.Kind == ElectricalDeviceKind.Panel)
        {
            yield return new SheetPolygon(Box(at, 0.35, 0.12), 0.35, ElectricalInk, ElectricalInk);
            yield return new SheetText(new Point2(at.X + 0.3, at.Y + 0.2), device.Name, 2.2, ElectricalInk, true);
            yield break;
        }
        if (device.IsLuminaire)
        {
            yield return new SheetPolygon(Circle(at, 0.2), 0.3, ElectricalInk, null);
            yield return new SheetPolyline([new(at.X - 0.14, at.Y - 0.14), new(at.X + 0.14, at.Y + 0.14)], 0.3, ElectricalInk);
            yield return new SheetPolyline([new(at.X - 0.14, at.Y + 0.14), new(at.X + 0.14, at.Y - 0.14)], 0.3, ElectricalInk);
            yield break;
        }
        if (device.IsSwitch)
        {
            yield return new SheetPolygon(Circle(at, 0.08), 0.3, ElectricalInk, ElectricalInk);
            yield return new SheetText(new Point2(at.X + 0.12, at.Y + 0.08), "S", 2.2, ElectricalInk, true);
            yield break;
        }
        // Receptacles and everything else: a circle with two prongs.
        yield return new SheetPolygon(Circle(at, 0.12), 0.3, ElectricalInk, null);
        yield return new SheetPolyline([new(at.X - 0.05, at.Y - 0.06), new(at.X - 0.05, at.Y + 0.06)], 0.3, ElectricalInk);
        yield return new SheetPolyline([new(at.X + 0.05, at.Y - 0.06), new(at.X + 0.05, at.Y + 0.06)], 0.3, ElectricalInk);
    }

    private static IEnumerable<SheetPrimitive> PlumbingPrimitives(BuildingDocument document, Guid levelId)
    {
        var elevation = document.Levels.FirstOrDefault(level => level.Id == levelId)?.Elevation ?? 0;
        if (document.PlumbingSystems.Count > 0)
            foreach (var report in PlumbingAnalysis.Analyze(document).Systems)
            {
                var ink = PipeInk(report.System.Kind);
                foreach (var run in report.Runs.Where(run => !run.Detail && run.From.DistanceTo(run.To) > 1e-6 && Math.Abs(run.FromZ - elevation) <= 1.2))
                    yield return new SheetPolyline([run.From, run.To], report.System.Kind == PlumbingSystemKind.Sanitary ? 0.5 : 0.35, ink,
                        report.System.Kind is not PlumbingSystemKind.ColdWater);
            }
        foreach (var point in document.PlumbingPoints.Where(point => point.LevelId == levelId))
        {
            yield return new SheetPolygon(Box(point.Position, 0.2, 0.2), 0.35, (0.1, 0.25, 0.6), null);
            yield return new SheetText(new Point2(point.Position.X + 0.3, point.Position.Y + 0.25), point.Name, 2.2, (0.1, 0.25, 0.6), true);
        }
    }

    private static (double, double, double) PipeInk(PlumbingSystemKind kind) => kind switch
    {
        PlumbingSystemKind.ColdWater => (0.1, 0.35, 0.8),
        PlumbingSystemKind.HotWater => (0.85, 0.25, 0.1),
        PlumbingSystemKind.StormWater => (0.1, 0.6, 0.7),
        _ => (0.45, 0.3, 0.15)
    };

    private static IEnumerable<SheetPrimitive> MechanicalPrimitives(BuildingDocument document, Guid levelId)
    {
        foreach (var element in document.StructuralElements.Where(element => element.LevelId == levelId &&
                     ViewDisciplines.Of(element) == ViewDiscipline.Mechanical))
            yield return new SheetPolygon(element.Footprint, 0.35, MechanicalInk, null);
        foreach (var unit in document.ElectricalDevices.Where(device => device.LevelId == levelId && device.Kind == ElectricalDeviceKind.MiniSplit))
        {
            yield return new SheetPolygon(Box(unit.Position, 0.45, 0.12), 0.35, MechanicalInk, null);
            yield return new SheetText(new Point2(unit.Position.X + 0.5, unit.Position.Y + 0.2), "A/C", 2.2, MechanicalInk, true);
        }
    }

    private static Point2[] Box(Point2 centre, double halfWidth, double halfDepth) =>
    [
        new(centre.X - halfWidth, centre.Y - halfDepth), new(centre.X + halfWidth, centre.Y - halfDepth),
        new(centre.X + halfWidth, centre.Y + halfDepth), new(centre.X - halfWidth, centre.Y + halfDepth)
    ];

    private static Point2[] Circle(Point2 centre, double radius) =>
        Enumerable.Range(0, 16).Select(step => new Point2(centre.X + radius * Math.Cos(step * Math.Tau / 16),
            centre.Y + radius * Math.Sin(step * Math.Tau / 16))).ToArray();

    private static Point2 Centre(IReadOnlyList<Point2> points) =>
        points.Count == 0 ? default : new Point2(points.Average(point => point.X), points.Average(point => point.Y));
}
