using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Synchronizes the render preview guide with the current BIM selection and shared camera.</summary>
    private void UpdateRenderSelection()
    {
        if (RenderPanelControl is null || _session is null) return;
        var selection = CurrentSelection();
        IReadOnlyList<EdgeSegment> edges = ViewTabs.SelectedIndex == RenderTabIndex && selection is { } selected
            ? Picker.Outline(selected.Kind, selected.Id) : [];
        RenderPanelControl.ShowSelection(edges);
    }
}
