using System.Windows;
using System.Windows.Controls;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>
/// Presents the complete architecture command surface and delegates execution
/// to the owning workspace.
/// </summary>
public partial class ArchitectureRibbon : UserControl
{
    /// <summary>
    /// Initializes the architecture ribbon and its localized command groups.
    /// </summary>
    public ArchitectureRibbon()
    {
        InitializeComponent();
        Loaded += (_, _) => AttachDemoToolTips(this);
    }

    /// <summary>Gives every tagged command button its rich tooltip once localized labels have resolved.</summary>
    private static void AttachDemoToolTips(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is Button { Tag: string commandId, Content: string label } button && button.ToolTip is not System.Windows.Controls.ToolTip)
                CommandDemoToolTip.Attach(button, commandId, label);
            AttachDemoToolTips(child);
        }
    }

    /// <summary>
    /// Occurs when the user selects an architecture command.
    /// </summary>
    public event EventHandler<ArchitectureCommandEventArgs>? CommandInvoked;

    /// <summary>
    /// Gets the wall type selector so the workspace can synchronize document types.
    /// </summary>
    public ComboBox WallTypePickerControl => WallTypePicker;

    /// <summary>
    /// Gets the modify command button so the workspace can reflect drawing mode.
    /// </summary>
    public Button WallCommandButtonControl => WallCommandButton;

    /// <summary>
    /// Converts the button tag to a typed command and notifies the workspace.
    /// </summary>
    private void ArchitectureButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string commandName } ||
            !Enum.TryParse(commandName, out ArchitectureCommand command))
        {
            return;
        }

        CommandInvoked?.Invoke(this, new ArchitectureCommandEventArgs(command));
    }
}
