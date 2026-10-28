using System.Windows;
using System.Windows.Controls;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>Localized application menu whose host owns document operations and history.</summary>
public partial class ApplicationFileMenu : ContextMenu
{
    /// <summary>Builds the menu without retaining a document or a main-window reference.</summary>
    public ApplicationFileMenu() => InitializeComponent();

    public event Action<string>? CommandInvoked;

    /// <summary>Forwards a menu command identifier to the owning application window.</summary>
    private void Command_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string command }) CommandInvoked?.Invoke(command);
    }
}
