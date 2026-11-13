using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Duplicates the selected slab, roof, ceiling, or terrain without modifying the original.</summary>
    private bool TryDuplicateFloor()
    {
        if (_selectedFloorId is not Guid floorId ||
            !_session.Document.Floors.Any(floor => floor.Id == floorId)) return false;
        _selectedFloorId = _session.DuplicateFloor(floorId);
        StatusText.Text = GetText("Status.FloorDuplicated");
        RefreshAll();
        return true;
    }
}
