using System.Windows;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Opens the project parameter manager from the main workspace.</summary>
public partial class MainWindow
{
    /// <summary>Shows the parameter manager focused on the category of the current selection.</summary>
    private void ProjectParameters_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProjectParametersWindow(_session, SelectedCategory()) { Owner = this };
        window.ShowDialog();
        RefreshAll();
    }

    /// <summary>
    /// Maps the explicit model selection to its parameter category. The sheet
    /// shown in the sheet workspace is display state, not a user selection, so
    /// it is deliberately ignored (it previously forced every open to Sheets).
    /// </summary>
    private ElementCategory? SelectedCategory()
    {
        if (_selectedOpeningId is Guid openingId)
        {
            var opening = _session.Document.Openings.FirstOrDefault(candidate => candidate.Id == openingId);
            if (opening is not null)
                return opening.Kind == OpeningKind.Door ? ElementCategory.Doors : ElementCategory.Windows;
        }
        if (_selectedWallId is not null) return ElementCategory.Walls;
        if (_selectedRoomId is not null) return ElementCategory.Rooms;
        if (_selectedFloorId is not null) return ElementCategory.Floors;
        return null;
    }
}
