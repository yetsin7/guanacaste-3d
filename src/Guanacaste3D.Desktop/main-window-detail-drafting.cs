using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private enum DetailDrawingMode { None, Line, Region }
    private DetailDrawingMode _detailDrawingMode;
    private Point2? _detailFirstPoint;
    private Point2? _detailPreviewPoint;
    private DetailFillPattern _detailPattern = DetailFillPattern.Herringbone;
    private DetailStrokeStyle _detailLineStyle = DetailStrokeStyle.Solid;
    private string _detailLineName = "paredes";

    private Guid ActiveDetailViewId()
    {
        if (_selectedViewId is Guid id &&
            _session.Document.Views.Any(v => v.Id == id && v.Kind == ProjectViewKind.Plan && v.LevelId == _activeLevelId))
            return id;
        return _session.Document.Views.First(v => v.Kind == ProjectViewKind.Plan && v.LevelId == _activeLevelId).Id;
    }

    private void BeginDetailDrawing(bool region)
    {
        CancelStructuralTool();
        SetWallTool(false);
        // Pick the drafting style before sketching, without touching model-space geometry.
        var options = new Window
        {
            Title = region ? "Estilo de región rellenada" : "Estilo de línea de detalle",
            Owner = this, Width = 360, Height = 190, MinWidth = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize
        };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = region ? "Patrón de relleno" : "Estilo de línea",
            Margin = new Thickness(0, 0, 0, 6) });
        var picker = new ComboBox { Margin = new Thickness(0, 0, 0, 14) };
        if (region)
        {
            foreach (var pattern in Enum.GetValues<DetailFillPattern>())
                picker.Items.Add(new ComboBoxItem { Content = pattern switch
                    {
                        DetailFillPattern.Solid => "Sólido",
                        DetailFillPattern.Diagonal => "Diagonal",
                        DetailFillPattern.Crosshatch => "Retícula cruzada",
                        DetailFillPattern.Herringbone => "Espiga (tipo Bóndex)",
                        _ => "Puntos"
                    }, Tag = pattern,
                    IsSelected = pattern == _detailPattern });
        }
        else
        {
            foreach (var style in Enum.GetValues<DetailStrokeStyle>())
                picker.Items.Add(new ComboBoxItem { Content = style switch
                    {
                        DetailStrokeStyle.Solid => "Continua · paredes",
                        DetailStrokeStyle.Dashed => "Discontinua · PISOS",
                        DetailStrokeStyle.Dotted => "Punteada · TEXTO",
                        _ => "Eje · SANITARIA"
                    }, Tag = style, IsSelected = style == _detailLineStyle });
        }
        panel.Children.Add(picker);
        var confirm = new Button { Content = "Comenzar a dibujar", MinWidth = 155,
            HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        confirm.Click += (_, _) => options.DialogResult = true;
        panel.Children.Add(confirm);
        options.Content = panel;
        if (options.ShowDialog() != true) return;
        if (picker.SelectedItem is ComboBoxItem picked)
        {
            if (region && picked.Tag is DetailFillPattern fill) _detailPattern = fill;
            if (!region && picked.Tag is DetailStrokeStyle stroke) _detailLineStyle = stroke;
        }
        _detailDrawingMode = region ? DetailDrawingMode.Region : DetailDrawingMode.Line;
        _detailFirstPoint = _detailPreviewPoint = null;
        ViewTabs.SelectedIndex = 0;
        PlanCanvas.Cursor = Cursors.Cross;
        StatusText.Text = region
            ? "Región rellenada: marque las dos esquinas. Clic derecho para cancelar."
            : "Línea de detalle: marque inicio y fin; continúe dibujando o clic derecho para salir.";
        RenderPlan();
    }

    private bool TryPlaceDetail(MouseButtonEventArgs e)
    {
        if (_detailDrawingMode == DetailDrawingMode.None) return false;
        var point = SnapWallPoint(CanvasToWorld(PlanPosition(e)));
        e.Handled = true;
        if (_detailFirstPoint is not Point2 start)
        {
            _detailFirstPoint = point;
            _detailPreviewPoint = point;
            RenderPlan();
            return true;
        }
        if (point.DistanceTo(start) < 0.001) return true;
        try
        {
            if (_detailDrawingMode == DetailDrawingMode.Line)
            {
                _session.AddDetailLine(ActiveDetailViewId(), start, point,
                    _detailLineName, "#202020", _detailLineStyle);
                _detailFirstPoint = point; // chain subsequent segments
            }
            else
            {
                _session.AddFilledRegion(ActiveDetailViewId(), [
                    start, new Point2(point.X, start.Y), point,
                    new Point2(start.X, point.Y)
                ], _detailPattern);
                _detailFirstPoint = null;
            }
            RefreshAll();
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(this, ex.Message, "Detalle 2D", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RenderPlan();
        return true;
    }

    private bool UpdateDetailDrawing(MouseEventArgs e)
    {
        if (_detailDrawingMode == DetailDrawingMode.None) return false;
        _detailPreviewPoint = SnapWallPoint(CanvasToWorld(PlanPosition(e)));
        RenderPlan();
        return true;
    }

    private bool CancelDetailDrawing()
    {
        if (_detailDrawingMode == DetailDrawingMode.None) return false;
        _detailDrawingMode = DetailDrawingMode.None;
        _detailFirstPoint = _detailPreviewPoint = null;
        PlanCanvas.Cursor = Cursors.Arrow;
        StatusText.Text = "Herramienta de detalle finalizada.";
        RenderPlan();
        return true;
    }

    /// <summary>Draws annotations last; their symbols are attached to their specific view id.</summary>
    private void RenderViewDetails()
    {
        var viewId = ActiveDetailViewId();
        foreach (var region in _session.Document.FilledRegions.Where(region => region.ViewId == viewId))
            DrawRegion(region);
        foreach (var detail in _session.Document.DetailLines.Where(line => line.ViewId == viewId))
        {
            var a = WorldToCanvas(detail.Start);
            var b = WorldToCanvas(detail.End);
            PlanCanvas.Children.Add(new Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = (Brush)new BrushConverter().ConvertFromString(detail.Color)!,
                StrokeThickness = detail.StrokeWidth,
                StrokeDashArray = DashFor(detail.StrokeStyle),
                Tag = detail.Id,
                ToolTip = detail.StyleName,
                SnapsToDevicePixels = true
            });
        }
        if (_detailFirstPoint is not Point2 from || _detailPreviewPoint is not Point2 to) return;
        if (_detailDrawingMode == DetailDrawingMode.Line)
        {
            var a = WorldToCanvas(from);
            var b = WorldToCanvas(to);
            PlanCanvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = Brushes.DodgerBlue, StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false });
        }
        else if (_detailDrawingMode == DetailDrawingMode.Region)
            DrawRegion(new FilledRegion(Guid.Empty, viewId, [
                from, new Point2(to.X, from.Y), to, new Point2(from.X, to.Y)
            ]) { Pattern = _detailPattern });
    }

    private static DoubleCollection? DashFor(DetailStrokeStyle style) => style switch
    {
        DetailStrokeStyle.Dashed => new DoubleCollection { 6, 3 },
        DetailStrokeStyle.Dotted => new DoubleCollection { 1, 3 },
        DetailStrokeStyle.Center => new DoubleCollection { 12, 3, 2, 3 },
        _ => null
    };

    private void DrawRegion(FilledRegion region)
    {
        var points = region.Boundary.Select(WorldToCanvas).ToArray();
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        var geometry = new PathGeometry(new[] { figure });
        var foreground = (Brush)new BrushConverter().ConvertFromString(region.Foreground)!;
        var background = (Brush)new BrushConverter().ConvertFromString(region.Background)!;
        var polygon = new System.Windows.Shapes.Path
        {
            Data = geometry, Fill = background,
            Stroke = region.ShowBoundary ? foreground : Brushes.Transparent,
            StrokeThickness = 1, Tag = region.Id == Guid.Empty ? null : region.Id,
            IsHitTestVisible = region.Id != Guid.Empty
        };
        PlanCanvas.Children.Add(polygon);
        if (region.Pattern == DetailFillPattern.Solid) return;
        var bounds = geometry.Bounds;
        var clip = geometry;
        var overlay = new Canvas { Width = PlanCanvas.ActualWidth, Height = PlanCanvas.ActualHeight,
            Clip = clip, IsHitTestVisible = false };
        var spacing = Math.Max(4, region.PatternSpacing * _scale);
        if (region.Pattern == DetailFillPattern.Dots)
        {
            for (var x = bounds.Left; x <= bounds.Right; x += spacing)
                for (var y = bounds.Top; y <= bounds.Bottom; y += spacing)
                {
                    var dot = new Ellipse { Width = 1.5, Height = 1.5, Fill = foreground };
                    Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y);
                    overlay.Children.Add(dot);
                    if (overlay.Children.Count >= 10000) break;
                }
        }
        else if (region.Pattern == DetailFillPattern.Herringbone)
        {
            // Short alternating strokes create a proper woven/herringbone bond,
            // clipped to the user-drawn region rather than the model geometry.
            var pitch = Math.Max(5, spacing * 0.70);
            var columns = Math.Min(300, (int)Math.Ceiling(bounds.Width / pitch) + 1);
            var rows = Math.Min(300, (int)Math.Ceiling(bounds.Height / pitch) + 1);
            for (var row = 0; row <= rows; row++)
                for (var column = 0; column <= columns; column++)
                {
                    var x = bounds.Left + column * pitch;
                    var y = bounds.Top + row * pitch;
                    var rise = ((column + row) & 1) == 0 ? pitch / 2 : -pitch / 2;
                    AddHatchLine(overlay, x, y + pitch / 2, x + pitch / 2,
                        y + pitch / 2 + rise, foreground);
                    AddHatchLine(overlay, x + pitch / 2, y + pitch / 2 + rise,
                        x + pitch, y + pitch / 2, foreground);
                }
        }
        else
        {
            var span = bounds.Width + bounds.Height;
            var count = Math.Min(2500, (int)Math.Ceiling(span / spacing) + 1);
            for (var i = -count; i <= count; i++)
            {
                var offset = i * spacing;
                AddHatchLine(overlay, bounds.Left + offset, bounds.Top,
                    bounds.Left + offset + span, bounds.Top + span, foreground);
                if (region.Pattern is DetailFillPattern.Crosshatch or DetailFillPattern.Herringbone)
                    AddHatchLine(overlay, bounds.Left + offset, bounds.Bottom,
                        bounds.Left + offset + span, bounds.Bottom - span, foreground);
            }
        }
        PlanCanvas.Children.Add(overlay);
    }

    private static void AddHatchLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush ink) =>
        canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = ink, StrokeThickness = 0.8 });
}
