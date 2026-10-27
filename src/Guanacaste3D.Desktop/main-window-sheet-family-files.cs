using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Recognizes native title-block .g3df containers before the legacy door/window reader handles family files.</summary>
    private bool TryImportSheetFamily(string path)
    {
        if (!NativeDocumentStore.TryInspect(path, out var info) || info?.Kind != NativeDocumentKind.Family) return false;
        var family = NativeDocumentStore.LoadFamily(path);
        if (family.Category != FamilyCategory.TitleBlocks) return false;
        _selectedSheetId = _session.AddSheetFromFamily(family);
        _selectedViewId = null;
        ViewTabs.SelectedIndex = 3;
        RefreshAll();
        StatusText.Text = FormatText("Status.FamilyImported", family.Name);
        return true;
    }
}
