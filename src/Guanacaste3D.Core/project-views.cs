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

/// <summary>Defines the model axis used to project elevation and section geometry.</summary>
public enum ViewOrientation
{
    None,
    North,
    East
}

/// <summary>Represents a persistent view generated from the shared building document.</summary>
public sealed record ProjectView(
    Guid Id,
    string Name,
    ProjectViewKind Kind,
    Guid? LevelId,
    ViewOrientation Orientation);

/// <summary>Creates the standard coordinated views included in every project.</summary>
public static class BuiltInProjectViews
{
    public static readonly Guid Model3DId = Guid.Parse("50802fa4-c8b4-44aa-a29f-68433e489130");
    public static readonly Guid NorthElevationId = Guid.Parse("03adbfd6-9c7d-4635-9f93-2645f1ac45e4");
    public static readonly Guid SectionAId = Guid.Parse("26870a61-1d20-4f66-918a-6138d5370687");
    public static readonly Guid ProjectScheduleId = Guid.Parse("54af0a0e-63f6-4fd8-9ff3-aa27947758e8");

    /// <summary>Creates plan views for all levels and the standard project-wide views.</summary>
    public static List<ProjectView> CreateDefaults(IReadOnlyCollection<Level> levels)
    {
        var views = levels.Select(CreatePlan).ToList();
        views.Add(new ProjectView(Model3DId, "Coordinated 3D", ProjectViewKind.Model3D, null, ViewOrientation.None));
        views.Add(new ProjectView(NorthElevationId, "North Elevation", ProjectViewKind.Elevation, null, ViewOrientation.North));
        views.Add(new ProjectView(SectionAId, "Section A", ProjectViewKind.Section, null, ViewOrientation.East));
        views.Add(new ProjectView(ProjectScheduleId, "Project Schedule", ProjectViewKind.Schedule, null, ViewOrientation.None));
        return views;
    }

    /// <summary>Creates the coordinated plan view owned by one level.</summary>
    public static ProjectView CreatePlan(Level level) => new(
        Guid.NewGuid(),
        $"{level.Name} Plan",
        ProjectViewKind.Plan,
        level.Id,
        ViewOrientation.None);
}
