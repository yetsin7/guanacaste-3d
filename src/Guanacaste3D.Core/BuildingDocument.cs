using System.Text.Json.Serialization;

namespace Guanacaste3D.Core;

/// <summary>Represents a position in the model's XY ground plane, measured in meters.</summary>
public readonly record struct Point2(double X, double Y)
{
    /// <summary>Returns the Euclidean metric distance to another point.</summary>
    public double DistanceTo(Point2 other) => double.Hypot(other.X - X, other.Y - Y);
}

/// <summary>A named vertical building datum, measured in meters from the project origin.</summary>
public sealed record Level(Guid Id, string Name, double Elevation)
{
    /// <summary>Protected base roles survive renaming and elevation changes.</summary>
    public LevelRole Role { get; init; }
}

/// <summary>A straight metric wall, tied to a building level.</summary>
public sealed record Wall(Guid Id, Guid LevelId, Point2 Start, Point2 End, double Thickness, double Height)
{
    public Guid WallTypeId { get; init; } = BuiltInWallTypes.Basic200Id;

    /// <summary>Level the wall top is tied to; null means the height is unconnected.</summary>
    public Guid? TopLevelId { get; init; }

    /// <summary>Distance in meters from the top level to the wall top (negative is below it).</summary>
    public double TopOffset { get; init; }

    /// <summary>Finish applied to both faces of the wall, used for finish area takeoff.</summary>
    public FinishKind Finish { get; init; }

    [JsonIgnore] public double Length => Start.DistanceTo(End);
}

/// <summary>Owns the editable architectural source of truth for all 2D and 3D views.</summary>
public sealed class BuildingDocument
{
    /// <summary>The schema written by this build; older files are migrated on load.</summary>
    public const int CurrentSchemaVersion = 15;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Project";
    public List<PlanDimension> Dimensions { get; set; } = [];
    public List<Callout> Callouts { get; set; } = [];
    public List<Level> Levels { get; set; } = [];
    public List<GridAxis> Grids { get; set; } = [];
    public List<Wall> Walls { get; set; } = [];
    public List<WallOpening> Openings { get; set; } = [];
    public List<WallType> WallTypes { get; set; } = [];
    public List<OpeningType> OpeningTypes { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
    public List<FloorSlab> Floors { get; set; } = [];
    public List<Column> Columns { get; set; } = [];
    public List<StructuralElement> StructuralElements { get; set; } = [];
    public List<DrawingSheet> Sheets { get; set; } = [];
    public List<FamilyDocument> SheetFamilies { get; set; } = [];
    public List<ProjectView> Views { get; set; } = [];
    public List<ViewTemplate> ViewTemplates { get; set; } = [];
    public List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
    public List<ParameterValue> ParameterValues { get; set; } = [];
    public List<ScheduleDefinition> Schedules { get; set; } = [];
    public List<FamilyDocument> InPlaceFamilies { get; set; } = [];
    public List<RenderMaterialAssignment> RenderMaterials { get; set; } = [];
    public List<ElectricalDevice> ElectricalDevices { get; set; } = [];
    public List<ElectricalCircuit> ElectricalCircuits { get; set; } = [];
    public List<PlumbingPoint> PlumbingPoints { get; set; } = [];
    public List<PlumbingSystem> PlumbingSystems { get; set; } = [];

    /// <summary>The plot for the location plan; null lets the plan propose one around the building.</summary>
    public SiteLot? Lot { get; set; }

    /// <summary>Creates a project with protected floor/roof datums and built-in architectural type catalogs.</summary>
    public static BuildingDocument Create(string name = "Untitled Project")
    {
        var levels = new List<Level>
        {
            new(Guid.NewGuid(), DefaultLabels.Get("LevelName", 1), 0) { Role = LevelRole.BaseFloor },
            new(Guid.NewGuid(), DefaultLabels.Get("LevelName", 2), 3) { Role = LevelRole.BaseRoof }
        };
        return new BuildingDocument
        {
            Name = name,
            Levels = levels,
            WallTypes = BuiltInWallTypes.CreateDefaults(),
            OpeningTypes = BuiltInOpeningTypes.CreateDefaults(),
            Views = BuiltInProjectViews.CreateDefaults(levels)
        };
    }

    /// <summary>Returns a deep-enough copy because contained entities are immutable records.</summary>
    public BuildingDocument Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        Id = Id,
        Name = Name,
        Dimensions = [.. Dimensions],
        Callouts = [.. Callouts ?? []],
        Levels = [.. Levels],
        Grids = [.. Grids],
        Walls = [.. Walls],
        Openings = [.. Openings],
        WallTypes = [.. WallTypes],
        OpeningTypes = [.. OpeningTypes],
        Rooms = [.. Rooms],
        Floors = Floors.Select(floor => floor with
        {
            Boundary = [.. floor.Boundary], TopoPoints = floor.TopoPoints is null ? null : [.. floor.TopoPoints]
        }).ToList(),
        Columns = [.. Columns],
        StructuralElements = [.. StructuralElements],
        Sheets = Sheets.Select(sheet => sheet with { Viewports = [.. sheet.Viewports] }).ToList(),
        SheetFamilies = SheetFamilies.Select(family => family.Clone()).ToList(),
        Views = [.. Views],
        ViewTemplates = [.. ViewTemplates],
        ParameterDefinitions = ParameterDefinitions
            .Select(definition => definition with { Categories = [.. definition.Categories] }).ToList(),
        ParameterValues = [.. ParameterValues],
        Schedules = Schedules.Select(schedule => schedule with
        {
            Fields = [.. schedule.Fields], Filters = [.. schedule.Filters], SortGroups = [.. schedule.SortGroups]
        }).ToList(),
        InPlaceFamilies = InPlaceFamilies.Select(family => family.Clone()).ToList(),
        RenderMaterials = [.. RenderMaterials],
        ElectricalDevices = ElectricalDevices.Select(device => device with { Controls = [.. device.Controls] }).ToList(),
        ElectricalCircuits = [.. ElectricalCircuits],
        PlumbingPoints = [.. PlumbingPoints],
        PlumbingSystems = PlumbingSystems.Select(system => system with { Fixtures = [.. system.Fixtures] }).ToList(),
        Lot = Lot
    };

    /// <summary>Rejects inconsistent projects before mutation, opening, or writing a file.</summary>
    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion) throw new InvalidDataException("Unsupported project schema version.");
        foreach (var view in Views) view.Graphics?.Validate();
        if (Lot is not null && !Lot.IsValid) throw new InvalidDataException("The site lot is invalid.");
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 120)
            throw new InvalidDataException("The project identity or name is invalid.");
        if (Dimensions is null || Levels is null || Grids is null || Walls is null || Openings is null || WallTypes is null ||
            Rooms is null || Floors is null || Columns is null || StructuralElements is null ||
            Sheets is null || Views is null ||
            Levels.Count is < 1 or > 200 || Walls.Count > 100000 || Openings.Count > 200000 ||
            Rooms.Count > 100000 || Floors.Count > 100000 || Columns.Count > 100000 ||
            StructuralElements.Count > 100000 ||
            Sheets.Count > 10000 || Views.Count > 10000)
            throw new InvalidDataException("The document exceeds the supported element limits.");
        if (Levels.Any(l => l is null))
            throw new InvalidDataException("A level cannot be null.");
        if (Levels.Any(l => l.Id == Guid.Empty || string.IsNullOrWhiteSpace(l.Name) ||
             l.Name.Length > 100 || !double.IsFinite(l.Elevation)) ||
            Levels.Select(l => l.Id).Distinct().Count() != Levels.Count)
            throw new InvalidDataException("The level collection is invalid.");
        var levelIds = Levels.Select(l => l.Id).ToHashSet();
        BaseLevels.Validate(this);
        if (Grids.Count > 10000 || Grids.Any(axis => axis is null || !axis.IsValid) ||
            Grids.Select(axis => axis.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Grids.Count)
            throw new InvalidDataException("Invalid grid axis definitions.");
        if (WallTypes.Count is < 1 or > 10000 || WallTypes.Any(type =>
                type.Id == Guid.Empty || string.IsNullOrWhiteSpace(type.Name) || type.Name.Length > 120 ||
                !Enum.IsDefined(type.Function) || !double.IsFinite(type.Thickness) ||
                type.Thickness is < 0.03 or > 5 || string.IsNullOrWhiteSpace(type.MaterialName) ||
                type.MaterialName.Length > 120 || !Enum.IsDefined(type.PlanFillPattern) ||
                !Enum.IsDefined(type.SectionFillPattern) || !IsRgbColor(type.PlanFillColor) ||
                !IsRgbColor(type.SectionFillColor)) ||
            WallTypes.Select(type => type.Id).Distinct().Count() != WallTypes.Count)
            throw new InvalidDataException("Invalid wall-type catalog.");
        var wallTypeIds = WallTypes.Select(type => type.Id).ToHashSet();
        if (Walls.Any(w => w is null) || Walls.Select(w => w.Id).Distinct().Count() != Walls.Count)
            throw new InvalidDataException("The wall collection contains duplicate identifiers.");
        foreach (var wall in Walls)
        {
            if (wall.Id == Guid.Empty || !levelIds.Contains(wall.LevelId) || !wallTypeIds.Contains(wall.WallTypeId) ||
                !double.IsFinite(wall.Start.X) || !double.IsFinite(wall.Start.Y) ||
                !double.IsFinite(wall.End.X) || !double.IsFinite(wall.End.Y) ||
                !double.IsFinite(wall.Thickness) || !double.IsFinite(wall.Height) ||
                wall.Length < 0.05 || wall.Length > 100000 ||
                wall.Thickness is < 0.05 or > 5 || wall.Height is < 0.1 or > 1000 ||
                !Enum.IsDefined(wall.Finish))
                throw new InvalidDataException("A wall is invalid or has no associated level.");
        }
        ValidateRooms(levelIds);
        ValidateFloors(levelIds);
        ValidateColumns(levelIds);
        ValidateSheets();
        DocumentSession.ValidateSheetLayouts(this);
        ValidateViews(levelIds);
        if (Dimensions.Any(item => item is null) || StructuralElements.Any(item => item is null))
            throw new InvalidDataException("Invalid plan dimension references.");
        var allIds = Levels.Select(level => level.Id)
            .Concat(Grids.Select(axis => axis.Id))
            .Concat(Walls.Select(wall => wall.Id))
            .Concat(Openings.Select(opening => opening.Id))
            .Concat(WallTypes.Select(type => type.Id))
            .Concat(Rooms.Select(room => room.Id))
            .Concat(Floors.Select(floor => floor.Id))
            .Concat(Columns.Select(column => column.Id))
            .Concat(Dimensions.Select(dimension => dimension.Id))
            .Concat((Callouts ?? []).Where(callout => callout is not null).Select(callout => callout.Id))
            .Concat(StructuralElements.Select(element => element.Id))
            .Concat(Sheets.Select(sheet => sheet.Id))
            .Concat(Sheets.SelectMany(sheet => sheet.Viewports).Select(viewport => viewport.Id))
            .Concat(SheetFamilies.Select(family => family.Id))
            .Concat(Views.Select(view => view.Id))
            .Concat((ParameterDefinitions ?? []).Select(definition => definition.Id))
            .Concat((OpeningTypes ?? []).Select(type => type.Id))
            .Concat((Schedules ?? []).Select(schedule => schedule.Id))
            .Concat((InPlaceFamilies ?? []).Where(family => family is not null).Select(family => family.Id))
            .ToArray();
        if (Openings.Any(o => o is null) || allIds.Distinct().Count() != allIds.Length)
            throw new InvalidDataException("Element identifiers must be unique.");
        WallGeometry.Validate(Walls, Openings);
        PlanDimensions.Validate(this);
        CalloutRules.Validate(this);
        CirculationGeometry.Validate(this);
        ArchitecturalShapes.Validate(this);
        OpeningTypeRules.Validate(this);
        LevelConstraints.Validate(this);
        ParameterRules.Validate(this);
        ScheduleDefinitionRules.Validate(this);
        InPlaceFamilyRules.Validate(this);
        RenderMaterialRules.Validate(this);
        ElectricalRules.Validate(this);
        PlumbingRules.Validate(this);
    }

    /// <summary>Checks the persisted color contract without coupling the core model to a UI framework.</summary>
    private static bool IsRgbColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

    /// <summary>Validates room identity, level ownership, dimensions, and labels.</summary>
    private void ValidateRooms(IReadOnlySet<Guid> levelIds)
    {
        if (Rooms.Any(room => room.Id == Guid.Empty || !levelIds.Contains(room.LevelId) ||
                string.IsNullOrWhiteSpace(room.Number) || room.Number.Length > 32 ||
                string.IsNullOrWhiteSpace(room.Name) || room.Name.Length > 120 ||
                !double.IsFinite(room.FirstCorner.X) || !double.IsFinite(room.FirstCorner.Y) ||
                !double.IsFinite(room.SecondCorner.X) || !double.IsFinite(room.SecondCorner.Y) ||
                room.Area is < 0.1 or > 100000000) ||
            Rooms.Select(room => room.Number).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Rooms.Count)
            throw new InvalidDataException("Invalid room definitions.");
    }

    /// <summary>Validates slab boundaries, dimensions, and level ownership.</summary>
    private void ValidateFloors(IReadOnlySet<Guid> levelIds)
    {
        foreach (var floor in Floors)
        {
            if (floor.Id == Guid.Empty || !levelIds.Contains(floor.LevelId) ||
                string.IsNullOrWhiteSpace(floor.Name) || floor.Name.Length > 120 ||
                floor.Boundary is null || floor.Boundary.Count is < 3 or > 500 ||
                floor.Boundary.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)) ||
                !double.IsFinite(floor.Thickness) || floor.Thickness is < 0.03 or > 3 ||
                !double.IsFinite(floor.ElevationOffset) || floor.ElevationOffset is < -10 or > 1000 ||
                !Enum.IsDefined(floor.Kind) ||
                floor.CalculateArea() is < 0.1 or > 100000000 || !Enum.IsDefined(floor.Finish) ||
                !double.IsFinite(floor.Overhang) || floor.Overhang is < 0 or > 5 ||
                (floor.Overhang > 0 && floor.Kind != FloorKind.Roof))
                throw new InvalidDataException("Invalid floor slab.");
            if (floor.TopoPoints is { } points &&
                (!floor.IsToposolid || points.Count > 2000 || points.Any(point =>
                    !double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Elevation) ||
                    point.Elevation is < -1000 or > 5000 ||
                    !Geometry.TopographyGeometry.Contains(floor.Boundary, point.Plan) &&
                    floor.Boundary.All(corner => corner.DistanceTo(point.Plan) > 0.05))))
                throw new InvalidDataException("Invalid toposolid point.");
        }
    }

    /// <summary>Validates column identity, level ownership, plan section, and height.</summary>
    private void ValidateColumns(IReadOnlySet<Guid> levelIds)
    {
        if (Columns.Any(column => column.Id == Guid.Empty || !levelIds.Contains(column.LevelId) ||
                !double.IsFinite(column.Position.X) || !double.IsFinite(column.Position.Y) ||
                !double.IsFinite(column.Width) || column.Width is < 0.05 or > 10 ||
                !double.IsFinite(column.Depth) || column.Depth is < 0.05 or > 10 ||
                !double.IsFinite(column.Height) || column.Height is < 0.1 or > 1000) ||
            Columns.Select(column => column.Id).Distinct().Count() != Columns.Count)
            throw new InvalidDataException("Invalid column definitions.");
    }

    /// <summary>Validates sheet identity, numbering, names, and paper sizes.</summary>
    private void ValidateSheets()
    {
        if (Sheets.Any(sheet => sheet.Id == Guid.Empty || string.IsNullOrWhiteSpace(sheet.Number) ||
                sheet.Number.Length > 32 || string.IsNullOrWhiteSpace(sheet.Name) ||
                sheet.Name.Length > 120 || !Enum.IsDefined(sheet.PaperSize)) ||
            Sheets.Select(sheet => sheet.Number).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Sheets.Count)
            throw new InvalidDataException("Invalid drawing sheets.");
    }

    /// <summary>Validates view identity, naming, kind, orientation, and level ownership.</summary>
    private void ValidateViews(IReadOnlySet<Guid> levelIds)
    {
        if (Views.Count < 1 || Views.Any(view => view.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(view.Name) || view.Name.Length > 120 ||
                !Enum.IsDefined(view.Kind) || !Enum.IsDefined(view.Orientation) ||
                (view.Cut is not null && (view.Kind != ProjectViewKind.Section || !view.Cut.IsValid)) ||
                (view.Kind == ProjectViewKind.Plan &&
                    (view.LevelId is not Guid levelId || !levelIds.Contains(levelId))) ||
                (view.Kind != ProjectViewKind.Plan && view.LevelId is not null)) ||
            Views.Select(view => view.Id).Distinct().Count() != Views.Count)
            throw new InvalidDataException("Invalid project views.");
    }
}
