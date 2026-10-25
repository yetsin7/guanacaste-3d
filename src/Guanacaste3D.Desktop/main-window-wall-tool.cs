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

    /// <summary>The last wall of a chained sketch and the reference point where its sketch segment ended.</summary>
    private (Guid WallId, Point2 ReferenceEnd)? _chainTail;

    /// <summary>True while Create Similar waits for the next wall picked in the plan.</summary>
    private bool _awaitingSimilarWallPick;

    /// <summary>Tracks the pointer in model space so the active wall sketch has immediate visual feedback.</summary>
    private void PlanCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (TryPanPlan(e)) return;
        TryStartElementDrag(e);
        if (_movingElement)
        {
            UpdateMovePreview(e.GetPosition(PlanCanvas));
            return;
        }
        if (TryUpdateStructuralPreview(e)) return;
        if (!_drawingWall) return;
        var pointer = CanvasToWorld(e.GetPosition(PlanCanvas));
        _wallPreviewPoint = SnapWallPoint(pointer);
        UpdateAlignmentGuides(pointer, null);
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
            ClearDirectWallLength();
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
        var thickness = _session.Document.WallTypes.Single(type => type.Id == typeId).Thickness;
        using var group = _session.BeginGroup("Transaction.DrawWall");
        if (FindContinuedWallEnd(referenceStart, typeId) is { } continued)
        {
            // Face and interior references offset each wall on its own, so a continued end only
            // meets where the two offset lines cross. Moving the existing end there closes the corner.
            var existing = continued.Wall;
            var meeting = WallPlacementGeometry.CenterlineIntersection(
                new WallCenterline(existing.Start, existing.End), new WallCenterline(start, end));
            if (meeting is Point2 corner && corner.DistanceTo(referenceStart) <= 2 * thickness)
            {
                var anchor = continued.End == WallEnd.Start ? existing.Start : existing.End;
                if (corner.DistanceTo(anchor) > WallJoinIndex.Tolerance)
                    _session.MoveWallEnd(existing.Id, continued.End, corner);
                start = corner;
            }
        }
        var id = _session.AddWall(_activeLevelId, start, end, height: height, wallTypeId: typeId);
        group.Assimilate();
        _selectedWallId = id;
        _chainTail = WallChainCheck.IsChecked == true ? (id, referenceEnd) : null;
        _selectedViewId = null;
        _pendingWallStart = WallChainCheck.IsChecked == true ? referenceEnd : null;
        _wallPreviewPoint = referenceEnd;
        ClearDirectWallLength();
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
        _chainTail = null;
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
            !double.TryParse(CurrentWallLengthText(), NumberStyles.Float, CultureInfo.CurrentCulture, out var length) ||
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
            ClearDirectWallLength();
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
                AddWallPreviewBody(corners[index], corners[(index + 1) % corners.Length]);
            AddWallMeasurement(start, preview, FormatText("WallOptions.RectangleMeasurement",
                Math.Abs(preview.X - start.X), Math.Abs(preview.Y - start.Y)));
            return;
        }

        var end = ResolveWallPreviewEndpoint(start, preview);
        AddWallPreviewBody(start, end);
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

    /// <summary>Adds a translucent full-thickness wall body to the current plan canvas.</summary>
    private void AddWallPreviewBody(Point2 start, Point2 end)
    {
        if (start.DistanceTo(end) < 0.05 || !TryReadWallPlacementOptions(out _, out var offset, out var typeId))
            return;
        var wallType = _session.Document.WallTypes.Single(type => type.Id == typeId);
        var footprint = WallPlacementGeometry.Footprint(
            start, end, wallType.Thickness, offset, SelectedWallLocationReference());
        PlanCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection(footprint.Points.Select(WorldToCanvas)),
            Fill = WallAppearanceRendering.CreateBrush(
                wallType.PlanFillColor, wallType.PlanFillPattern, 0.72),
            Stroke = new SolidColorBrush(Color.FromRgb(0, 96, 124)),
            StrokeThickness = 1.5,
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
        ClearDirectWallLength();
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
            ClearDirectWallLength();
            StatusText.Text = GetText("Status.WallTool");
            RenderPlan();
            return;
        }
        SetWallTool(false);
    }

    /// <summary>Applies Revit-like two-stage escape behavior to the active placement command.</summary>
    private void HandleWallEscape() => CancelWallPlacementStep();

    /// <summary>
    /// Returns the same-type wall end that a new wall starts from: the chained tail when it is
    /// continued, otherwise any wall on the level whose endpoint sits on the reference point.
    /// </summary>
    private (Wall Wall, WallEnd End)? FindContinuedWallEnd(Point2 referenceStart, Guid typeId)
    {
        if (_chainTail is { } tail && tail.ReferenceEnd.DistanceTo(referenceStart) < 1e-9 &&
            _session.Document.Walls.FirstOrDefault(candidate => candidate.Id == tail.WallId) is Wall previous &&
            previous.WallTypeId == typeId)
            return (previous, WallEnd.End);

        foreach (var wall in _session.Document.Walls)
        {
            if (wall.LevelId != _activeLevelId || wall.WallTypeId != typeId) continue;
            if (wall.End.DistanceTo(referenceStart) <= WallJoinIndex.Tolerance) return (wall, WallEnd.End);
            if (wall.Start.DistanceTo(referenceStart) <= WallJoinIndex.Tolerance) return (wall, WallEnd.Start);
        }
        return null;
    }

    /// <summary>Arms Create Similar; the next wall picked in the plan supplies the type and height of the new wall.</summary>
    private void CreateSimilar_Click(object sender, RoutedEventArgs e)
    {
        SetWallTool(false);
        _awaitingSimilarWallPick = true;
        StatusText.Text = GetText("Status.CreateSimilarPick");
    }

    /// <summary>Starts the wall tool with the type and height of the wall picked for Create Similar.</summary>
    private void TryStartSimilarWall(object source)
    {
        if ((source as FrameworkElement)?.Tag is not Guid id ||
            _session.Document.Walls.FirstOrDefault(candidate => candidate.Id == id) is not Wall picked)
        {
            StatusText.Text = GetText("Status.CreateSimilarPick");
            return;
        }

        _awaitingSimilarWallPick = false;
        SetWallTool(true);
        WallTypePicker.SelectedItem = WallTypePicker.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag is Guid typeId && typeId == picked.WallTypeId);
        WallHeightInput.Text = picked.Height.ToString("0.00", CultureInfo.CurrentCulture);
    }

    /// <summary>Shows or hides contextual placement controls so command state is always visible.</summary>
    private void UpdateWallToolVisualState()
    {
        if (WallOptionsBar is null) return;
        WallOptionsBar.Visibility = _drawingWall ? Visibility.Visible : Visibility.Collapsed;
        WallCommandButton.FontWeight = _drawingWall ? FontWeights.Bold : FontWeights.Normal;
        if (!_drawingWall) _activeWallSnap = null;
    }
}
