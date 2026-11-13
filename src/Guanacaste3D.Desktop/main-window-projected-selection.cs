using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Highlights the complete projected profile of the selected building element.</summary>
    private void ApplyProjectedSelection(Shape shape, Guid elementId)
    {
        if (CurrentSelection()?.Id != elementId) return;
        shape.Fill = new SolidColorBrush(Color.FromArgb(135, 0, 115, 230));
        shape.Stroke = new SolidColorBrush(Color.FromRgb(0, 90, 200));
        shape.StrokeThickness = 2;
    }

    /// <summary>Selects a projected element and exposes its shared property editor without changing its geometry.</summary>
    private void ProjectedElement_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not Shape { Tag: Guid id }) return;
        var document = _session.Document;
        var opening = document.Openings.FirstOrDefault(item => item.Id == id);
        if (opening is not null)
        {
            SelectExtendedElement();
            _selectedWallId = opening.WallId;
            _selectedOpeningId = opening.Id;
            _selectedWallOffset = opening.Offset;
        }
        else if (document.Walls.Any(item => item.Id == id))
        {
            SelectExtendedElement();
            _selectedWallId = id;
            _selectedWallOffset = null;
        }
        else if (document.Floors.Any(item => item.Id == id)) SelectExtendedElement(floorId: id);
        else if (document.Columns.Any(item => item.Id == id)) SelectExtendedElement(columnId: id);
        else if (document.StructuralElements.Any(item => item.Id == id)) SelectExtendedElement(structuralId: id);
        else return;
        _viewSelection = null;
        RefreshProperties();
        RenderOrthographicViews();
        e.Handled = true;
        if (e.ClickCount >= 2) OpenSelectionEditor();
    }
}
