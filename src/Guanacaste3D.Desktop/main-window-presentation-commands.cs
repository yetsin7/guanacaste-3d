using System.Windows;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Routes presentation commands to the shared camera, live renderer, and project material editor.</summary>
    private bool TryExecutePresentationCommand(string commandId)
    {
        switch (commandId)
        {
            case "Materials":
                ViewTabs.SelectedIndex = RenderTabIndex;
                RenderPanelControl.FocusMaterials();
                return true;
            case "Walkthrough":
                ViewTabs.SelectedIndex = RenderTabIndex;
                RenderPanelControl.FocusWalkthrough();
                return true;
            case "RealtimeVisualization":
                ViewTabs.SelectedIndex = RenderTabIndex;
                return true;
            case "Camera":
                ViewTabs.SelectedIndex = Model3DTabIndex;
                ModelSurface.Focus();
                return true;
            case "ResetCamera":
                ViewTabs.SelectedIndex = Model3DTabIndex;
                FrameCameraToScene();
                return true;
            case "VisualStyle":
                ViewTabs.SelectedIndex = Model3DTabIndex;
                ClayCheck.IsChecked = ClayCheck.IsChecked != true;
                return true;
            default:
                return false;
        }
    }
}
