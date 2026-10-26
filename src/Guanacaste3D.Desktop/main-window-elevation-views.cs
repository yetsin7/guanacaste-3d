using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Elevation symbols in the plan, as in Revit: a circle with an arrow at each side of the building, one per elevation
/// view. Click one to select it, double-click to open the view, and rename it from its menu, the project browser or F2.
/// The Elevation tab shows whichever elevation was chosen last.
/// </summary>
public partial class MainWindow
{
    private Guid _elevationViewId = BuiltInProjectViews.NorthElevationId;

    /// <summary>Distance, in meters, between the building and its elevation symbols.</summary>
    private const double ElevationSymbolDistance = 3.0;

    private static readonly Brush ElevationSymbolBrush = Frozen(Color.FromRgb(70, 70, 76));
    private static readonly Brush ElevationSelectedBrush = Frozen(Color.FromRgb(240, 130, 20));

    /// <summary>The elevation views, with the plan position and look direction of their symbols.</summary>
    private IEnumerable<(ProjectView View, Point2 Position, Point2 Direction)> ElevationSymbols()
    {
        var document = _session.Document;
        var points = document.Walls.SelectMany(wall => new[] { wall.Start, wall.End })
            .Concat(document.Floors.SelectMany(floor => floor.Boundary))
            .Concat(document.Columns.Select(column => column.Position)).ToList();
        if (_links.Bounds() is { } linked)
        {
            points.Add(new Point2(linked.Min.X, linked.Min.Y));
            points.Add(new Point2(linked.Max.X, linked.Max.Y));
        }
        double minX = -5, maxX = 5, minY = -5, maxY = 5;
        if (points.Count > 0)
        {
            minX = points.Min(point => point.X);
            maxX = points.Max(point => point.X);
            minY = points.Min(point => point.Y);
            maxY = points.Max(point => point.Y);
        }
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        foreach (var view in document.Views.Where(view => view.Kind == ProjectViewKind.Elevation))
        {
            yield return view.Orientation switch
            {
                ViewOrientation.North => (view, new Point2(centerX, minY - ElevationSymbolDistance), new Point2(0, 1)),
                ViewOrientation.South => (view, new Point2(centerX, maxY + ElevationSymbolDistance), new Point2(0, -1)),
                ViewOrientation.East => (view, new Point2(minX - ElevationSymbolDistance, centerY), new Point2(1, 0)),
                _ => (view, new Point2(maxX + ElevationSymbolDistance, centerY), new Point2(-1, 0))
            };
        }
    }

    /// <summary>Draws every elevation symbol on the plan, the selected one in orange.</summary>
    private void RenderElevationSymbols()
    {
        foreach (var (view, position, direction) in ElevationSymbols())
        {
            var selected = _selectedViewId == view.Id;
            var brush = selected ? ElevationSelectedBrush : ElevationSymbolBrush;
            var center = WorldToCanvas(position);
            const double radius = 13;
            var along = new Vector(direction.X, -direction.Y);
            var across = new Vector(-along.Y, along.X);
            var menu = CreateViewMenu(view.Id);

            var circle = new Ellipse
            {
                Width = 2 * radius, Height = 2 * radius, Fill = Brushes.White, Stroke = brush, StrokeThickness = 1.6,
                Tag = view.Id, ContextMenu = menu, ToolTip = view.Name
            };
            Canvas.SetLeft(circle, center.X - radius);
            Canvas.SetTop(circle, center.Y - radius);
            PlanCanvas.Children.Add(circle);

            var tip = center + along * (radius * 2.1);
            var arrow = new Polygon
            {
                Points = [tip, center + along * (radius * 0.3) + across * (radius * 0.8),
                    center + along * (radius * 0.3) - across * (radius * 0.8)],
                Fill = brush, Stroke = brush, StrokeThickness = 1, Tag = view.Id, ContextMenu = menu, ToolTip = view.Name
            };
            PlanCanvas.Children.Add(arrow);

            var label = new TextBlock
            {
                Text = view.Name, FontSize = 11, Foreground = brush, Background = Brushes.White, Padding = new Thickness(3, 0, 3, 0),
                Tag = view.Id, ContextMenu = menu, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            // The label goes on the side away from the arrow, so it never covers the building.
            var anchor = center - along * (radius + 4 + label.DesiredSize.Height / 2);
            Canvas.SetLeft(label, anchor.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, anchor.Y - label.DesiredSize.Height / 2);
            PlanCanvas.Children.Add(label);
        }
    }

    private ContextMenu CreateViewMenu(Guid viewId)
    {
        var open = new MenuItem { Header = GetText("Elevation.Open") };
        open.Click += (_, _) => SelectProjectView(viewId);
        var rename = new MenuItem { Header = GetText("Elevation.Rename"), InputGestureText = "F2" };
        rename.Click += (_, _) => RenameView(viewId);
        return new ContextMenu { Items = { open, rename } };
    }

    /// <summary>Selects an elevation symbol clicked in the plan; a double-click opens its view.</summary>
    private bool TrySelectElevationSymbol(MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.Tag is not Guid id ||
            _session.Document.Views.FirstOrDefault(view => view.Id == id) is not { Kind: ProjectViewKind.Elevation }) return false;
        if (e.ClickCount >= 2)
        {
            SelectProjectView(id);
            return true;
        }
        _elevationViewId = id;
        _selectedViewId = id;
        _selectedWallId = null;
        _selectedOpeningId = null;
        _selectedFloorId = _selectedRoomId = _selectedColumnId = _selectedStructuralId = null;
        RefreshProperties();
        RenderPlan();
        return true;
    }

    /// <summary>Asks for a new name for a view and applies it as one undoable step.</summary>
    private void RenameView(Guid id)
    {
        if (_session.Document.Views.FirstOrDefault(view => view.Id == id) is not { } current) return;
        var name = PromptWindow.Ask(this, GetText("Elevation.RenameTitle"), current.Name);
        if (name is null) return;
        try
        {
            _session.RenameView(id, name);
            StatusText.Text = GetText("Status.ViewRenamed");
            RefreshAll();
            RenderOrthographicViews();
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Elevation.RenameTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>The orientation and name of the elevation the Elevation tab is showing.</summary>
    private (ViewOrientation Orientation, string Name) CurrentElevation()
    {
        var view = _session.Document.Views.FirstOrDefault(item => item.Id == _elevationViewId && item.Kind == ProjectViewKind.Elevation)
            ?? _session.Document.Views.FirstOrDefault(item => item.Kind == ProjectViewKind.Elevation);
        return view is null ? (ViewOrientation.North, GetText("View.Elevation")) : (view.Orientation, view.Name);
    }
}
