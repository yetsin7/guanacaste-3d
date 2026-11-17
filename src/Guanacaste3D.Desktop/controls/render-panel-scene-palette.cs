using System.Windows;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// The floating tool palette over the interactive Render view starts folded to its four main tools, so it no longer hides
/// the model; the «more tools» button unfolds the rest and folds it again.
/// </summary>
public partial class RenderPanel
{
    private void SceneToolsToggle_Click(object sender, RoutedEventArgs e) =>
        SceneToolsExtended.Visibility = SceneToolsExtended.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
}
