using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Copies a selected column while retaining its type and level.</summary>
    private bool TryDuplicateColumn()
    {
        if (_selectedColumnId is not Guid columnId ||
            !_session.Document.Columns.Any(column => column.Id == columnId)) return false;

        _selectedColumnId = _session.DuplicateColumn(columnId);
        StatusText.Text = GetText("Status.ColumnDuplicated");
        RefreshAll();
        return true;
    }

    /// <summary>Copies a selected component through the same undoable operation used in the render workspace.</summary>
    private bool TryDuplicateComponent()
    {
        if (_selectedStructuralId is not Guid id ||
            !_session.Document.StructuralElements.Any(element => element.Id == id &&
                element.Kind == StructuralElementKind.Component)) return false;

        _selectedStructuralId = _session.DuplicateRenderComponent(id);
        StatusText.Text = GetText("Status.ComponentDuplicated");
        RefreshAll();
        return true;
    }
}
