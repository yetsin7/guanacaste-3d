using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Guanacaste3D.Desktop.Controls;

public partial class RenderPanel
{
    /// <summary>Reveals the existing project material controls after the Render sidebar is laid out.</summary>
    internal void FocusMaterials() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
    {
        MaterialRows.BringIntoView();
        MaterialRows.Children.OfType<DockPanel>().FirstOrDefault()?.Children.OfType<ComboBox>().FirstOrDefault()?.Focus();
    }));

    /// <summary>Expands the walkthrough editor and reveals its camera marks without changing the camera.</summary>
    internal void FocusWalkthrough() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
    {
        TourExpander.IsExpanded = true;
        TourExpander.BringIntoView();
        MarksList.Focus();
    }));
}
