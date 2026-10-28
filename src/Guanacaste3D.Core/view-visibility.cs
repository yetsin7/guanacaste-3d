namespace Guanacaste3D.Core;

/// <summary>
/// Per-view hiding and isolation. A view draws a copy of the building without the elements it hides, so the same
/// building can show different things in different views.
/// </summary>
/// <summary>A saved set of hidden and isolated elements that any view can take on, one at a time or many at once.</summary>
public sealed record ViewTemplate(Guid Id, string Name, List<Guid> HiddenElementIds, List<Guid> IsolatedElementIds);

public static class ViewVisibility
{
    /// <summary>
    /// Returns a copy of the document that keeps only what the view shows. With no view, or a view that hides and
    /// isolates nothing, the copy is complete.
    /// </summary>
    public static BuildingDocument Apply(BuildingDocument document, ProjectView? view)
    {
        var copy = document.Clone();
        if (view is null || (view.HiddenElementIds.Count == 0 && view.IsolatedElementIds.Count == 0)) return copy;

        bool Shown(Guid id) => !view.HiddenElementIds.Contains(id) &&
                               (view.IsolatedElementIds.Count == 0 || view.IsolatedElementIds.Contains(id));

        copy.Walls.RemoveAll(wall => !Shown(wall.Id));
        // An opening belongs to its wall: it shows with the wall unless it is hidden itself.
        copy.Openings.RemoveAll(opening => !Shown(opening.Id) || !copy.Walls.Any(wall => wall.Id == opening.WallId));
        copy.Floors.RemoveAll(floor => !Shown(floor.Id));
        copy.Columns.RemoveAll(column => !Shown(column.Id));
        copy.StructuralElements.RemoveAll(element => !Shown(element.Id));
        return copy;
    }
}
