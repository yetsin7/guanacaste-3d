namespace Guanacaste3D.Core;

/// <summary>Plumbing design edits; each public method is one undoable transaction.</summary>
public sealed partial class DocumentSession
{
    /// <summary>Places a water meter or a sewer outlet.</summary>
    public Guid AddPlumbingPoint(Guid levelId, PlumbingPointKind kind, string name, Point2 position)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddPlumbingPoint", document => document.PlumbingPoints.Add(new PlumbingPoint(id, levelId, kind, name, position)));
        return id;
    }

    /// <summary>Removes a meter or outlet and the systems that start from it.</summary>
    public void RemovePlumbingPoint(Guid pointId)
    {
        if (Document.PlumbingPoints.All(point => point.Id != pointId)) return;
        Commit("Transaction.RemovePlumbingPoint", document =>
        {
            document.PlumbingPoints.RemoveAll(point => point.Id == pointId);
            document.PlumbingSystems.RemoveAll(system => system.SourceId == pointId);
        });
    }

    /// <summary>Moves a meter or outlet; its systems are routed again from the new position.</summary>
    public void MovePlumbingPoint(Guid pointId, Point2 position) =>
        UpdatePoint("Transaction.MovePlumbingPoint", pointId, point => point with { Position = position });

    /// <summary>Sets the static pressure of the street main at a meter, in kilopascals.</summary>
    public void SetSupplyPressure(Guid pointId, double kilopascals)
    {
        if (!double.IsFinite(kilopascals) || kilopascals is < 50 or > 1000) throw new InvalidDataException("Invalid plumbing point.");
        UpdatePoint("Transaction.SetSupplyPressure", pointId, point => point with { SupplyPressureKPa = kilopascals });
    }

    /// <summary>Sets how deep below the floor the drain leaves the building at an outlet, in meters.</summary>
    public void SetOutletDepth(Guid pointId, double depth)
    {
        if (!double.IsFinite(depth) || depth is < 0.2 or > 5) throw new InvalidDataException("Invalid plumbing point.");
        UpdatePoint("Transaction.SetOutletDepth", pointId, point => point with { InvertDepth = depth });
    }

    /// <summary>Starts a system from a meter (cold), a water heater (hot) or an outlet (sanitary).</summary>
    public Guid AddPlumbingSystem(PlumbingSystemKind kind, Guid sourceId, string name)
    {
        CheckSource(kind, sourceId);
        var id = Guid.NewGuid();
        Commit("Transaction.AddPlumbingSystem", document => document.PlumbingSystems.Add(new PlumbingSystem(id, kind, sourceId, name)));
        return id;
    }

    /// <summary>Connects a fixture to a system, or disconnects it when it is already on it; true when now connected.</summary>
    public bool TogglePlumbingFixture(Guid systemId, Guid fixtureId)
    {
        var system = Document.PlumbingSystems.FirstOrDefault(candidate => candidate.Id == systemId)
            ?? throw new InvalidDataException("The plumbing system does not exist.");
        var fixture = Document.StructuralElements.FirstOrDefault(element => element.Id == fixtureId);
        if (fixture is null || PlumbingStandards.Fixture(fixture) is not { } spec ||
            !(system.Kind switch
            {
                PlumbingSystemKind.ColdWater => spec.NeedsCold || PlumbingStandards.IsWaterHeater(fixture),
                PlumbingSystemKind.HotWater => spec.NeedsHot,
                _ => spec.Drains
            }))
            throw new InvalidDataException("The fixture does not use this system.");
        var connect = !system.Fixtures.Contains(fixtureId);
        Commit(connect ? "Transaction.ConnectFixture" : "Transaction.DisconnectFixture", document =>
        {
            // A fixture is on one system of each kind: connecting it moves it from any other.
            for (var index = 0; index < document.PlumbingSystems.Count; index++)
            {
                var candidate = document.PlumbingSystems[index];
                if (candidate.Kind != system.Kind) continue;
                var fixtures = candidate.Fixtures.Where(id => id != fixtureId).ToList();
                if (connect && candidate.Id == systemId) fixtures.Add(fixtureId);
                document.PlumbingSystems[index] = candidate with { Fixtures = fixtures };
            }
        });
        return connect;
    }

    public void RenamePlumbingSystem(Guid systemId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80) throw new InvalidDataException("Invalid plumbing system.");
        Commit("Transaction.RenamePlumbingSystem", document =>
        {
            var index = document.PlumbingSystems.FindIndex(system => system.Id == systemId);
            if (index < 0) throw new InvalidDataException("The plumbing system does not exist.");
            document.PlumbingSystems[index] = document.PlumbingSystems[index] with { Name = name.Trim() };
        });
    }

    public void RemovePlumbingSystem(Guid systemId) =>
        Commit("Transaction.RemovePlumbingSystem", document => document.PlumbingSystems.RemoveAll(system => system.Id == systemId));

    /// <summary>
    /// Connects every fixture in one step, as Revit's "Generate layout": cold water from the nearest meter to every
    /// fixture and water heater, hot water from the nearest heater to every fixture that uses it, and the drain from
    /// every fixture to the nearest outlet. Existing systems are replaced. Returns the number of systems made.
    /// </summary>
    public int ConnectPlumbing(Func<PlumbingSystemKind, int, string> name)
    {
        var meters = Document.PlumbingPoints.Where(point => point.Kind == PlumbingPointKind.WaterMeter).ToArray();
        var outlets = Document.PlumbingPoints.Where(point => point.Kind == PlumbingPointKind.SewerOutlet).ToArray();
        var heaters = Document.StructuralElements.Where(PlumbingStandards.IsWaterHeater).ToArray();
        var fixtures = Document.StructuralElements.Where(element => PlumbingStandards.Fixture(element) is not null).ToArray();
        if (meters.Length == 0 && outlets.Length == 0) throw new InvalidDataException("Place a water meter or a sewer outlet first.");
        var elevations = Document.Levels.ToDictionary(level => level.Id, level => level.Elevation);
        double Distance(Point2 a, Guid levelA, Point2 b, Guid levelB) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(elevations[levelA] - elevations[levelB]) * 2;

        var systems = new List<PlumbingSystem>();
        void Build(PlumbingSystemKind kind, IReadOnlyList<(Guid Id, Guid Level, Point2 At)> sources,
            Func<StructuralElement, bool> uses)
        {
            if (sources.Count == 0) return;
            var groups = fixtures.Where(uses)
                .GroupBy(fixture => sources.MinBy(source =>
                    Distance(source.At, source.Level, PlumbingStandards.Connection(fixture), fixture.LevelId)).Id);
            foreach (var group in groups)
                systems.Add(new PlumbingSystem(Guid.NewGuid(), kind, group.Key, name(kind, systems.Count(system => system.Kind == kind) + 1))
                {
                    Fixtures = group.Select(fixture => fixture.Id).ToList()
                });
        }
        Build(PlumbingSystemKind.ColdWater, meters.Select(meter => (meter.Id, meter.LevelId, meter.Position)).ToArray(), fixture =>
            PlumbingStandards.Fixture(fixture)!.NeedsCold || PlumbingStandards.IsWaterHeater(fixture));
        Build(PlumbingSystemKind.HotWater, heaters.Select(heater => (heater.Id, heater.LevelId, PlumbingStandards.Connection(heater))).ToArray(),
            fixture => PlumbingStandards.Fixture(fixture)!.NeedsHot);
        Build(PlumbingSystemKind.Sanitary, outlets.Select(outlet => (outlet.Id, outlet.LevelId, outlet.Position)).ToArray(), fixture =>
            PlumbingStandards.Fixture(fixture)!.Drains);
        Commit("Transaction.ConnectPlumbing", document =>
        {
            document.PlumbingSystems.Clear();
            document.PlumbingSystems.AddRange(systems);
        });
        return systems.Count;
    }

    private void CheckSource(PlumbingSystemKind kind, Guid sourceId)
    {
        var valid = kind switch
        {
            PlumbingSystemKind.ColdWater => Document.PlumbingPoints.Any(point => point.Id == sourceId && point.Kind == PlumbingPointKind.WaterMeter),
            PlumbingSystemKind.HotWater => Document.StructuralElements.Any(element => element.Id == sourceId && PlumbingStandards.IsWaterHeater(element)),
            _ => Document.PlumbingPoints.Any(point => point.Id == sourceId && point.Kind == PlumbingPointKind.SewerOutlet)
        };
        if (!valid) throw new InvalidDataException("The plumbing source does not exist.");
    }

    private void UpdatePoint(string transaction, Guid pointId, Func<PlumbingPoint, PlumbingPoint> change) =>
        Commit(transaction, document =>
        {
            var index = document.PlumbingPoints.FindIndex(point => point.Id == pointId);
            if (index < 0) throw new InvalidDataException("The plumbing point does not exist.");
            document.PlumbingPoints[index] = change(document.PlumbingPoints[index]);
        });
}

/// <summary>Structural rules of the plumbing data, checked with the rest of the document.</summary>
public static class PlumbingRules
{
    public static void Validate(BuildingDocument document)
    {
        if (document.PlumbingPoints is null || document.PlumbingSystems is null ||
            document.PlumbingPoints.Count > 1000 || document.PlumbingSystems.Count > 2000)
            throw new InvalidDataException("Invalid plumbing point.");
        var levels = document.Levels.Select(level => level.Id).ToHashSet();
        if (document.PlumbingPoints.Select(point => point.Id).Distinct().Count() != document.PlumbingPoints.Count)
            throw new InvalidDataException("Invalid plumbing point.");
        foreach (var point in document.PlumbingPoints)
            if (point.Id == Guid.Empty || !levels.Contains(point.LevelId) || !Enum.IsDefined(point.Kind) ||
                string.IsNullOrWhiteSpace(point.Name) || point.Name.Length > 120 ||
                !double.IsFinite(point.Position.X) || !double.IsFinite(point.Position.Y) ||
                !double.IsFinite(point.SupplyPressureKPa) || point.SupplyPressureKPa is < 50 or > 1000 ||
                !double.IsFinite(point.InvertDepth) || point.InvertDepth is < 0.2 or > 5)
                throw new InvalidDataException("Invalid plumbing point.");
        if (document.PlumbingSystems.Select(system => system.Id).Distinct().Count() != document.PlumbingSystems.Count)
            throw new InvalidDataException("Invalid plumbing system.");
        // A system whose heater or fixtures were deleted stays valid: the analysis skips what is missing.
        foreach (var system in document.PlumbingSystems)
            if (system.Id == Guid.Empty || system.SourceId == Guid.Empty || !Enum.IsDefined(system.Kind) ||
                string.IsNullOrWhiteSpace(system.Name) || system.Name.Length > 80 ||
                system.Fixtures is null || system.Fixtures.Count > 10000)
                throw new InvalidDataException("Invalid plumbing system.");
    }
}
