using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    /// <summary>Highlights the actual visible shapes of the selected elements without blocking subsequent picking.</summary>
    private void RenderPlanSelectionHighlight()
    {
        var selected = _viewSelection is { Count: > 0 } many ? many.ToHashSet() : new HashSet<Guid>();
        if (selected.Count == 0 &&
            (_selectedOpeningId ?? _selectedColumnId ?? _selectedStructuralId ?? _selectedRoomId ??
             _selectedFloorId ?? _selectedElectricalId ?? _selectedPlumbingId ?? _selectedWallId) is Guid id)
            selected.Add(id);
        if (selected.Count == 0) return;
        var fill = new SolidColorBrush(Color.FromArgb(125, 0, 110, 230));
        var outline = new SolidColorBrush(Color.FromRgb(0, 100, 220));
        foreach (var child in PlanCanvas.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not Guid elementId || !selected.Contains(elementId)) continue;
            if (child is Shape shape)
            {
                if (shape.Fill is not null) shape.Fill = fill;
                shape.Stroke = outline;
                shape.StrokeThickness = Math.Max(shape.StrokeThickness, 1.5);
            }
            else if (child is TextBlock label) label.Foreground = outline;
        }
    }
}
