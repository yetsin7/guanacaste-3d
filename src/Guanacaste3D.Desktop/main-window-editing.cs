using System.Globalization;
using System.IO;
using System.Windows;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Duplicates the selected wall with a visible metric offset.</summary>
    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id) { StatusText.Text = GetText("Status.SelectWall"); return; }
        _selectedWallId = _session.DuplicateWall(id, 1, 1);
        _selectedWallOffset = null;
        StatusText.Text = GetText("Status.WallDuplicated");
        RefreshAll();
    }

    /// <summary>Rotates the selected wall by the common 90-degree increment.</summary>
    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id)
        {
            StatusText.Text = GetText("Status.SelectWallToRotate");
            return;
        }
        try
        {
            _session.RotateWall(id, 90);
            _selectedWallOffset = null;
            StatusText.Text = GetText("Status.WallRotated90");
            RefreshAll();
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.RotateWall"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Starts the Revit-style move tool: pick a base point, then a destination in the plan.</summary>
    private void Move_Click(object sender, RoutedEventArgs e) => BeginMoveTool();

    /// <summary>Applies the metric translation entered in the properties panel.</summary>
    private void MovePrecise_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id) { StatusText.Text = GetText("Status.SelectWall"); return; }
        if (!double.TryParse(MoveXInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var deltaX) ||
            !double.TryParse(MoveYInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var deltaY) ||
            !double.IsFinite(deltaX) || !double.IsFinite(deltaY))
        {
            MessageBox.Show(this, GetText("Dialog.InvalidMove"), GetText("Dialog.MoveWall"));
            return;
        }
        try
        {
            _session.MoveWall(id, deltaX, deltaY);
            _selectedWallOffset = null;
            StatusText.Text = FormatText("Status.WallMoved", deltaX, deltaY);
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.MoveWall"));
        }
    }

    /// <summary>Applies the arbitrary rotation entered in the properties panel.</summary>
    private void RotatePrecise_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id) { StatusText.Text = GetText("Status.SelectWall"); return; }
        if (!double.TryParse(RotationInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var angle) ||
            !double.IsFinite(angle))
        {
            MessageBox.Show(this, GetText("Dialog.InvalidAngle"), GetText("Dialog.RotateWall"));
            return;
        }
        try
        {
            _session.RotateWall(id, angle);
            _selectedWallOffset = null;
            StatusText.Text = FormatText("Status.WallRotated", angle);
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.RotateWall"));
        }
    }

    /// <summary>Applies hosted-opening dimensions after parsing the property fields.</summary>
    private void ApplyOpening_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOpeningId is not Guid id)
        {
            StatusText.Text = GetText("Status.SelectOpening");
            return;
        }
        if (!double.TryParse(OpeningOffsetInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var offset) ||
            !double.TryParse(OpeningWidthInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var width) ||
            !double.TryParse(OpeningSillInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var sill) ||
            !double.TryParse(OpeningHeightInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var height))
        {
            MessageBox.Show(this, GetText("Dialog.InvalidOpeningDimensions"), GetText("Dialog.Opening"));
            return;
        }
        try
        {
            _session.UpdateOpening(id, offset, width, sill, height);
            StatusText.Text = GetText("Status.OpeningUpdated");
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.InvalidOpeningParameters"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Removes the selected hosted opening.</summary>
    private void RemoveOpening_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOpeningId is not Guid id) return;
        _session.RemoveOpening(id);
        _selectedOpeningId = null;
        StatusText.Text = GetText("Status.OpeningRemoved");
    }

    /// <summary>Deletes the selected supported BIM element and its dependent geometry.</summary>
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGridId is Guid gridId)
        {
            _session.RemoveGridAxis(gridId);
            _selectedGridId = null;
            RefreshAll();
            return;
        }
        if (_selectedOpeningId is Guid openingId)
        {
            // Selecting a door also selects its host wall for the properties panel; Delete removes only the door.
            // Deleting a wall is what takes the openings hosted in it along.
            _session.RemoveOpening(openingId);
            _selectedOpeningId = null;
            _selectedWallId = null;
            _selectedWallOffset = null;
            StatusText.Text = GetText("Status.OpeningRemoved");
            RefreshAll();
            return;
        }
        if (DeleteExtendedSelection()) return;
        if (_selectedWallId is not Guid id) return;
        _session.RemoveWall(id);
        _selectedWallId = null;
        RefreshAll();
    }
}
