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

    /// <summary>Copies a selected component or independent structural member through an undoable operation.</summary>
    private bool TryDuplicateComponent()
    {
        if (_selectedStructuralId is not Guid id ||
            _session.Document.StructuralElements.FirstOrDefault(element => element.Id == id) is not { } element)
            return false;
        if (element.Kind == StructuralElementKind.Component)
            _selectedStructuralId = _session.DuplicateRenderComponent(id);
        else if (element.Kind is StructuralElementKind.Beam or StructuralElementKind.Brace or StructuralElementKind.Truss or
                 StructuralElementKind.WallFoundation or StructuralElementKind.IsolatedFoundation)
            _selectedStructuralId = _session.DuplicateStructuralMember(id);
        else return false;
        StatusText.Text = GetText(element.Kind == StructuralElementKind.Component
            ? "Status.ComponentDuplicated" : "Status.StructuralMemberDuplicated");
        RefreshAll();
        return true;
    }
}
