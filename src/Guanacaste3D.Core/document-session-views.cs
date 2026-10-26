namespace Guanacaste3D.Core;

/// <summary>Naming of the project's views (plans, elevations, sections, schedules and the 3D view).</summary>
public sealed partial class DocumentSession
{
    /// <summary>
    /// Renames a view, as the elevation symbol or the project browser does. Names are trimmed and must be unique, as in
    /// Revit, so a view can always be told apart in the browser and on sheets.
    /// </summary>
    public void RenameView(Guid id, string name)
    {
        var view = Document.Views.FirstOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidDataException("The view does not exist.");
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120) throw new InvalidDataException("The view name is not valid.");
        if (name == view.Name) return;
        if (Document.Views.Any(other => other.Id != id && string.Equals(other.Name, name, StringComparison.CurrentCultureIgnoreCase)))
            throw new InvalidDataException("The view name is already in use.");
        Commit("Transaction.RenameView", document =>
        {
            var index = document.Views.FindIndex(candidate => candidate.Id == id);
            document.Views[index] = document.Views[index] with { Name = name };
        });
    }
}
