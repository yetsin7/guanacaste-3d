namespace Guanacaste3D.Core;

/// <summary>Classifies coordinated model and documentation views.</summary>
public enum ProjectViewKind
{
    Plan,
    Model3D,
    Elevation,
    Section,
    Schedule
}

/// <summary>
/// The direction an elevation or section looks toward. A north elevation shows the north face of the building, seen
/// from the south; its symbol in the plan sits south of the building and points north.
/// </summary>
public enum ViewOrientation
{
    None,
    North,
    East,
    South,
    West
}

/// <summary>Represents a persistent view generated from the shared building document.</summary>
public sealed record ProjectView(
    Guid Id,
    string Name,
    ProjectViewKind Kind,
    Guid? LevelId,
    ViewOrientation Orientation)
{
    /// <summary>Finite cutting line in model coordinates; null preserves the legacy centered section.</summary>
    public SectionCut? Cut { get; init; }
}

/// <summary>Creates the standard coordinated views included in every project.</summary>
public static class BuiltInProjectViews
{
    public static readonly Guid Model3DId = Guid.Parse("50802fa4-c8b4-44aa-a29f-68433e489130");
    public static readonly Guid NorthElevationId = Guid.Parse("03adbfd6-9c7d-4635-9f93-2645f1ac45e4");
    public static readonly Guid SectionAId = Guid.Parse("26870a61-1d20-4f66-918a-6138d5370687");
    public static readonly Guid ProjectScheduleId = Guid.Parse("54af0a0e-63f6-4fd8-9ff3-aa27947758e8");

    /// <summary>The area table view: rooms grouped by level, placeable on a sheet.</summary>
    public static readonly Guid AreaScheduleId = Guid.Parse("8d2f6a14-3b7e-4c59-a1d0-5e9c7b3f2a68");

    /// <summary>The location plan view: plot, street, neighbours and north, placeable on a sheet.</summary>
    public static readonly Guid LocationId = Guid.Parse("b41c9e07-6d25-4f83-8a1e-3c7d5f9b0e12");

    /// <summary>Whether a view is a generated drawing that sheets can place without a model scale of its own.</summary>
    public static bool IsGeneratedSheetView(ProjectView view) => view.Id == AreaScheduleId || view.Id == LocationId;

    public static readonly Guid SouthElevationId = Guid.Parse("7a1f0c52-5d4b-4f0e-8a9c-2b6e1d3f4a05");
    public static readonly Guid EastElevationId = Guid.Parse("c3d8e1b7-92a4-4c6f-b5e0-0f7a2d9c4b16");
    public static readonly Guid WestElevationId = Guid.Parse("e5b9a0d4-1c37-48fa-9d62-6a3c8f1e7b27");

    /// <summary>Default names of the standard views, as a Spanish Revit template calls them.</summary>
    public const string Model3DName = "{3D}", SectionName = "Sección 1", ScheduleName = "Tabla de cantidades";

    /// <summary>Names the four standard elevations carry in a new project; the user may rename any of them.</summary>
    public static readonly IReadOnlyDictionary<ViewOrientation, string> ElevationNames = new Dictionary<ViewOrientation, string>
    {
        [ViewOrientation.North] = "Vista Norte", [ViewOrientation.South] = "Vista Sur",
        [ViewOrientation.East] = "Vista Este", [ViewOrientation.West] = "Vista Oeste"
    };

    /// <summary>Creates plan views for all levels and the standard project-wide views.</summary>
    public static List<ProjectView> CreateDefaults(IReadOnlyCollection<Level> levels)
    {
        var views = levels.Select(CreatePlan).ToList();
        views.Add(new ProjectView(Model3DId, Model3DName, ProjectViewKind.Model3D, null, ViewOrientation.None));
        views.Add(new ProjectView(SectionAId, SectionName, ProjectViewKind.Section, null, ViewOrientation.East));
        views.Add(new ProjectView(ProjectScheduleId, ScheduleName, ProjectViewKind.Schedule, null, ViewOrientation.None));
        EnsureElevations(views);
        EnsureAreaSchedule(views);
        EnsureLocation(views);
        return views;
    }

    /// <summary>Adds the location plan view to projects made before it existed.</summary>
    public static void EnsureLocation(List<ProjectView> views)
    {
        if (views.All(view => view.Id != LocationId))
            views.Add(new ProjectView(LocationId, DefaultLabels.Get("Location.Title"), ProjectViewKind.Schedule, null,
                ViewOrientation.None));
    }

    /// <summary>Adds the area table view to projects made before it existed.</summary>
    public static void EnsureAreaSchedule(List<ProjectView> views)
    {
        if (views.All(view => view.Id != AreaScheduleId))
            views.Add(new ProjectView(AreaScheduleId, DefaultLabels.Get("AreaTable.Title"), ProjectViewKind.Schedule, null,
                ViewOrientation.None));
    }

    /// <summary>
    /// Makes sure the four elevations exist. Projects made before south, east and west elevations were offered get them;
    /// the old, unrenamed "North Elevation" takes its new name. A name the user chose is never touched.
    /// </summary>
    public static void EnsureElevations(List<ProjectView> views)
    {
        var ids = new Dictionary<ViewOrientation, Guid>
        {
            [ViewOrientation.North] = NorthElevationId, [ViewOrientation.South] = SouthElevationId,
            [ViewOrientation.East] = EastElevationId, [ViewOrientation.West] = WestElevationId
        };
        foreach (var (orientation, id) in ids)
        {
            var index = views.FindIndex(view => view.Id == id);
            if (index < 0)
                views.Add(new ProjectView(id, ElevationNames[orientation], ProjectViewKind.Elevation, null, orientation));
            else if (views[index].Name == "North Elevation")
                views[index] = views[index] with { Name = ElevationNames[orientation] };
        }
        // Projects made before the views were named in Spanish keep any name the user chose; only the untouched
        // English defaults are translated.
        var englishDefaults = new Dictionary<Guid, (string Old, string New)>
        {
            [Model3DId] = ("Coordinated 3D", Model3DName), [SectionAId] = ("Section A", SectionName),
            [ProjectScheduleId] = ("Project Schedule", ScheduleName)
        };
        for (var index = 0; index < views.Count; index++)
            if (englishDefaults.TryGetValue(views[index].Id, out var names) && views[index].Name == names.Old)
                views[index] = views[index] with { Name = names.New };
    }

    /// <summary>Creates the coordinated plan view owned by one level; like Revit, it carries the level's name.</summary>
    public static ProjectView CreatePlan(Level level) => new(
        Guid.NewGuid(),
        level.Name,
        ProjectViewKind.Plan,
        level.Id,
        ViewOrientation.None);
}
