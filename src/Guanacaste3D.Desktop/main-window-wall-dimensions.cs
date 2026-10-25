using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Wall dimension fields in the properties panel. Only fields the user changed are
/// sent as edits, so an untouched field never overrides a value that was solved
/// from another one (for example, the area after a length edit).
/// </summary>
public partial class MainWindow
{
    /// <summary>Field texts as they were loaded from the selected wall: length, height, thickness, area.</summary>
    private string[] _loadedWallDimensionTexts = ["", "", "", ""];

    /// <summary>Fills the dimension fields from a wall and remembers the loaded text of each one.</summary>
    private void LoadWallDimensionFields(Wall wall)
    {
        PropertyLengthInput.Text = wall.Length.ToString("0.00", CultureInfo.CurrentCulture);
        HeightInput.Text = wall.Height.ToString("0.00", CultureInfo.CurrentCulture);
        ThicknessInput.Text = wall.Thickness.ToString("0.00", CultureInfo.CurrentCulture);
        PropertyAreaInput.Text = (wall.Length * wall.Height).ToString("0.00", CultureInfo.CurrentCulture);
        _loadedWallDimensionTexts =
            [PropertyLengthInput.Text, HeightInput.Text, ThicknessInput.Text, PropertyAreaInput.Text];
        if (FinishPicker.Items.Count == 0)
            FinishPicker.ItemsSource = Enum.GetValues<FinishKind>()
                .Select(kind => new LabeledValue<FinishKind>(GetText($"Finish.{kind}"), kind)).ToArray();
        FinishPicker.SelectedValue = wall.Finish;
    }

    /// <summary>Applies the changed dimension fields with their locks as one solved transaction.</summary>
    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id)
        {
            StatusText.Text = GetText("Status.SelectWallToModify");
            return;
        }
        if (!TryReadChangedNumber(PropertyLengthInput, 0, out var length) ||
            !TryReadChangedNumber(HeightInput, 1, out var height) ||
            !TryReadChangedNumber(ThicknessInput, 2, out var thickness) ||
            !TryReadChangedNumber(PropertyAreaInput, 3, out var area))
        {
            MessageBox.Show(this, GetText("Dialog.InvalidWallDimensions"), GetText("Dialog.Parameters"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var locks = new WallDimensionLocks(
            Length: LockLengthCheck.IsChecked == true,
            Height: LockHeightCheck.IsChecked == true,
            Thickness: LockThicknessCheck.IsChecked == true);
        try
        {
            _session.ApplyWallDimensions(id, new WallDimensionEdit(length, height, thickness, area, locks));
            if (FinishPicker.SelectedValue is FinishKind finish &&
                _session.Document.Walls.FirstOrDefault(wall => wall.Id == id) is { } current &&
                current.Finish != finish)
                _session.SetWallFinish(id, finish);
            StatusText.Text = GetText("Status.WallDimensionsApplied");
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.InvalidParameters"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Returns success with a null value when the field is untouched, and success with the
    /// parsed value when it changed. Returns false when the changed text is not a finite number.
    /// </summary>
    private bool TryReadChangedNumber(TextBox input, int field, out double? value)
    {
        value = null;
        if (input.Text == _loadedWallDimensionTexts[field]) return true;
        if (!double.TryParse(input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) ||
            !double.IsFinite(parsed))
            return false;
        value = parsed;
        return true;
    }
}
