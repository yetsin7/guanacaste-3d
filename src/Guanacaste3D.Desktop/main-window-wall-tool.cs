using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Contains the contextual, preview-driven wall placement workflow.</summary>
public partial class MainWindow
{
    private Point2? _wallPreviewPoint;
    private WallSnapResult? _activeWallSnap;

    /// <summary>Tracks the pointer in model space so the active wall sketch has immediate visual feedback.</summary>
    private void PlanCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawingWall) return;
        _wallPreviewPoint = SnapWallPoint(CanvasToWorld(e.GetPosition(PlanCanvas)));
        RenderPlan();
    }

    /// <summary>Handles either endpoint placement or a two-corner rectangular wall operation.</summary>
    private void HandleWallPlacementClick(Point canvasPoint)
    {
        var point = SnapWallPoint(CanvasToWorld(canvasPoint));
        if (_pendingWallStart is null)
        {
            _pendingWallStart = point;
            _wallPreviewPoint = point;
            StatusText.Text = GetText("Status.WallSecondPoint");
            RenderPlan();
            return;
        }

        if (!TryReadWallPlacementOptions(out var height, out var offset, out var typeId))
        {
            StatusText.Text = GetText("Status.WallInvalidOptions");
            return;
        }

        var start = _pendingWallStart.Value;
        try
        {
            if (WallRectangleMode.IsChecked == true)
                CreateWallRectangle(start, point, height, offset, typeId);
            else
                CreateWallSegment(start, ConstrainWallEndpoint(start, point), height, offset, typeId);
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.InvalidGeometry"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Creates a line wall and optionally carries its reference endpoint into a chained segment.</summary>
    private void CreateWallSegment(Point2 referenceStart, Point2 referenceEnd, double height, double offset, Guid typeId)
    {
        var (start, end) = OffsetWallCenterline(referenceStart, referenceEnd, offset, typeId);
        _selectedWallId = _session.AddWall(_activeLevelId, start, end, height: height, wallTypeId: typeId);
        _selectedViewId = null;
        _pendingWallStart = WallChainCheck.IsChecked == true ? referenceEnd : null;
        _wallPreviewPoint = referenceEnd;
        StatusText.Text = GetText("Status.WallCreated");
        RefreshAll();
    }

    /// <summary>Creates four joined walls in one undoable transaction from opposite reference corners.</summary>
    private void CreateWallRectangle(Point2 first, Point2 opposite, double height, double offset, Guid typeId)
    {
        var type = _session.Document.WallTypes.Single(candidate => candidate.Id == typeId);
        var centerlines = WallPlacementGeometry.Rectangle(
            first, opposite, type.Thickness, offset, SelectedWallLocationReference());

        using var group = _session.BeginGroup("Transaction.DrawRectangle");
        foreach (var centerline in centerlines)
            _selectedWallId = _session.AddWall(
                _activeLevelId, centerline.Start, centerline.End,
                height: height, wallTypeId: typeId);
        group.Assimilate();
        _selectedViewId = null;
        _pendingWallStart = null;
        _wallPreviewPoint = null;
        StatusText.Text = GetText("Status.WallRectangleCreated");
        RefreshAll();
    }

    /// <summary>Snaps a reference point to the active ten-centimeter drawing grid.</summary>
    private Point2 SnapWallPoint(Point2 point)
    {
        if (WallObjectSnapCheck.IsChecked == true)
        {
            var wallCenterlines = _session.Document.Walls
                .Where(wall => wall.LevelId == _activeLevelId)
                .Select(wall => new WallCenterline(wall.Start, wall.End));
            _activeWallSnap = WallPlacementGeometry.FindNearestSnap(
                point, wallCenterlines, Math.Max(0.02, 12 / _scale));
            if (_activeWallSnap is WallSnapResult snap) return snap.Point;
        }
        _activeWallSnap = null;
        return SnapGridCheck.IsChecked == true
            ? new Point2(
                Math.Round(point.X * 10, MidpointRounding.AwayFromZero) / 10,
                Math.Round(point.Y * 10, MidpointRounding.AwayFromZero) / 10)
            : point;
    }

    /// <summary>Locks a line endpoint to the dominant axis when orthogonal placement is enabled.</summary>
    private Point2 ConstrainWallEndpoint(Point2 start, Point2 candidate)
        => WallPlacementGeometry.ConstrainEndpoint(start, candidate, WallOrthogonalCheck.IsChecked == true);

    /// <summary>Moves the stored centerline according to the chosen reference face and explicit offset.</summary>
    private (Point2 Start, Point2 End) OffsetWallCenterline(
        Point2 start, Point2 end, double offset, Guid typeId)
    {
        var thickness = _session.Document.WallTypes.Single(type => type.Id == typeId).Thickness;
        var centerline = WallPlacementGeometry.OffsetLine(
            start, end, thickness, offset, SelectedWallLocationReference());
        return (centerline.Start, centerline.End);
    }

    /// <summary>Maps the localized placement selector to a stable domain reference.</summary>
    private WallLocationReference SelectedWallLocationReference() =>
        ((WallLocationLinePicker.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "Exterior" => WallLocationReference.ExteriorFace,
            "Interior" => WallLocationReference.InteriorFace,
            _ => WallLocationReference.Centerline
        };

    /// <summary>Validates localized numeric placement inputs without mutating the document.</summary>
    private bool TryReadWallPlacementOptions(out double height, out double offset, out Guid typeId)
    {
        var validHeight = double.TryParse(WallHeightInput.Text, NumberStyles.Float,
            CultureInfo.CurrentCulture, out height) && height is >= 0.1 and <= 1000;
        var validOffset = double.TryParse(WallOffsetInput.Text, NumberStyles.Float,
            CultureInfo.CurrentCulture, out offset) && double.IsFinite(offset) && Math.Abs(offset) <= 100000;
        typeId = (WallTypePicker.SelectedItem as ComboBoxItem)?.Tag as Guid? ?? Guid.Empty;
        return validHeight && validOffset && typeId != Guid.Empty;
    }

    /// <summary>Creates a line wall at the exact entered length along the current preview direction.</summary>
    private void PlaceWallByLength_Click(object sender, RoutedEventArgs e)
    {
        if (WallRectangleMode.IsChecked == true)
        {
            StatusText.Text = GetText("Status.WallLengthRequiresLine");
            return;
        }
        if (_pendingWallStart is not Point2 start || _wallPreviewPoint is not Point2 directionCandidate)
        {
            StatusText.Text = GetText("Status.WallLengthNeedsDirection");
            return;
        }
        if (!TryReadWallPlacementOptions(out var height, out var offset, out var typeId) ||
            !double.TryParse(WallLengthInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var length) ||
            !double.IsFinite(length) || length is < 0.05 or > 100000)
        {
            StatusText.Text = GetText("Status.WallInvalidOptions");
            return;
        }

        try
        {
            var endpoint = WallPlacementGeometry.EndpointAtLength(
                start, directionCandidate, length, WallOrthogonalCheck.IsChecked == true);
            CreateWallSegment(start, endpoint, height, offset, typeId);
            WallLengthInput.SelectAll();
            WallLengthInput.Focus();
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), GetText("Dialog.InvalidGeometry"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Places the exact-length wall when Enter is pressed in the contextual length field.</summary>
    private void WallLengthInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        PlaceWallByLength_Click(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    /// <summary>Draws the provisional line or rectangle and its live dimensions over the plan.</summary>
    private void RenderWallSketchOverlay()
    {
        if (!_drawingWall) return;
        if (_activeWallSnap is WallSnapResult snap) AddWallSnapMarker(snap);
        if (_pendingWallStart is not Point2 start || _wallPreviewPoint is not Point2 preview) return;
        if (WallRectangleMode.IsChecked == true)
        {
            var corners = new[]
            {
                start, new Point2(preview.X, start.Y), preview, new Point2(start.X, preview.Y)
            };
            for (var index = 0; index < corners.Length; index++)
                AddWallPreviewLine(corners[index], corners[(index + 1) % corners.Length]);
            AddWallMeasurement(start, preview, FormatText("WallOptions.RectangleMeasurement",
                Math.Abs(preview.X - start.X), Math.Abs(preview.Y - start.Y)));
            return;
        }

        var end = ConstrainWallEndpoint(start, preview);
        AddWallPreviewLine(start, end);
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180 / Math.PI;
        var length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        AddWallMeasurement(start, end, FormatText("WallOptions.Measurement", length, angle));
    }

    /// <summary>Draws the active endpoint or midpoint snap marker beside its localized label.</summary>
    private void AddWallSnapMarker(WallSnapResult snap)
    {
        var center = WorldToCanvas(snap.Point);
        Shape marker;
        if (snap.Kind == WallSnapKind.Endpoint)
        {
            marker = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
                StrokeThickness = 2
            };
        }
        else
        {
            marker = new Polygon
            {
                Points = new PointCollection
                {
                    new(center.X, center.Y - 6), new(center.X + 6, center.Y),
                    new(center.X, center.Y + 6), new(center.X - 6, center.Y)
                },
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
                StrokeThickness = 2
            };
        }
        if (marker is Rectangle)
        {
            Canvas.SetLeft(marker, center.X - 5);
            Canvas.SetTop(marker, center.Y - 5);
        }
        marker.IsHitTestVisible = false;
        PlanCanvas.Children.Add(marker);

        var label = new TextBlock
        {
            Text = GetText(snap.Kind == WallSnapKind.Endpoint ? "WallSnap.Endpoint" : "WallSnap.Midpoint"),
            Foreground = new SolidColorBrush(Color.FromRgb(0, 96, 124)),
            Background = Brushes.White,
            FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, center.X + 9);
        Canvas.SetTop(label, center.Y + 7);
        PlanCanvas.Children.Add(label);
    }

    /// <summary>Adds a dashed cyan reference line to the current plan canvas.</summary>
    private void AddWallPreviewLine(Point2 start, Point2 end)
    {
        var from = WorldToCanvas(start);
        var to = WorldToCanvas(end);
        PlanCanvas.Children.Add(new Line
        {
            X1 = from.X,
            Y1 = from.Y,
            X2 = to.X,
            Y2 = to.Y,
            Stroke = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            IsHitTestVisible = false
        });
    }

    /// <summary>Places a compact dimension callout at the midpoint of a provisional wall sketch.</summary>
    private void AddWallMeasurement(Point2 start, Point2 end, string text)
    {
        var midpoint = WorldToCanvas(new Point2((start.X + end.X) / 2, (start.Y + end.Y) / 2));
        var label = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 127, 163)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(7, 3, 7, 3),
            Child = new TextBlock { Text = text, FontSize = 11, Foreground = Brushes.DarkSlateGray },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, midpoint.X + 8);
        Canvas.SetTop(label, midpoint.Y - 28);
        PlanCanvas.Children.Add(label);
    }

    /// <summary>Refreshes the provisional sketch when a contextual placement option changes.</summary>
    private void WallOptions_Changed(object sender, RoutedEventArgs e)
    {
        if (WallObjectSnapCheck?.IsChecked != true) _activeWallSnap = null;
        if (PlanCanvas is not null && _session is not null) RenderPlan();
    }

    /// <summary>Leaves wall placement while retaining all completed wall transactions.</summary>
    private void FinishWallTool_Click(object sender, RoutedEventArgs e) => SetWallTool(false);

    /// <summary>Cancels the provisional wall and leaves the wall command.</summary>
    private void CancelWallTool_Click(object sender, RoutedEventArgs e)
    {
        _pendingWallStart = null;
        _wallPreviewPoint = null;
        _activeWallSnap = null;
        SetWallTool(false);
    }

    /// <summary>Cancels the active segment first and exits the wall command on the next cancellation.</summary>
    private void CancelWallPlacementStep()
    {
        if (_pendingWallStart is not null)
        {
            _pendingWallStart = null;
            _wallPreviewPoint = null;
            _activeWallSnap = null;
            StatusText.Text = GetText("Status.WallTool");
            RenderPlan();
            return;
        }
        SetWallTool(false);
    }

    /// <summary>Applies Revit-like two-stage escape behavior to the active placement command.</summary>
    private void HandleWallEscape() => CancelWallPlacementStep();

    /// <summary>Shows or hides contextual placement controls so command state is always visible.</summary>
    private void UpdateWallToolVisualState()
    {
        if (WallOptionsBar is null) return;
        WallOptionsBar.Visibility = _drawingWall ? Visibility.Visible : Visibility.Collapsed;
        WallCommandButton.FontWeight = _drawingWall ? FontWeights.Bold : FontWeights.Normal;
        if (!_drawingWall) _activeWallSnap = null;
    }
}
