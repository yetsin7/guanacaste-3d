using System.Windows;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Routes presentation commands to the model camera and view settings.</summary>
    private bool TryExecutePresentationCommand(string commandId)
    {
        switch (commandId)
        {
            case "Decal":
                ShowDecalWindow();
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
                XRayCheck.IsChecked = XRayCheck.IsChecked != true;
                return true;
            default:
                return false;
        }
    }
}
