using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.CrossPlatform;

/// <summary>
/// Portable 2D plan work area. All geometry edits go through the shared BIM DocumentSession,
/// not through UI-local copies. The view works without a dedicated GPU.
/// </summary>
public sealed class PlanWorkspace : Grid
{
    public enum EditTool { Select, Wall, GridAxis }

    private readonly Canvas _canvas = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _inspector = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
    private readonly ListBox _browser = new();
    private readonly TextBox _axisName = new() { PlaceholderText = "Nombre del eje", Width = 165 };
    private readonly Button _rename = new() { Content = "Renombrar eje" };
    private readonly Button _delete = new() { Content = "Eliminar seleccionado" };
    private readonly Button _undo = new() { Content = "Deshacer" };
    private readonly Button _redo = new() { Content = "Rehacer" };
    private DocumentSession? _session;
    private Guid? _selectedId;
    private Point2? _firstPoint;
    private EditTool _tool;
    private double _scale = 43;
    private Point _offset = new(0, 0);
    private Point? _panPointer;
    private Point2? _previewWorldPoint;
    private PlanSnapping.SnapKind _previewSnapKind;
    private readonly CheckBox _snapEndpoints = new() { Content = "Extremos de muros y ejes", IsChecked = true };
    private readonly CheckBox _snapGrid = new() { Content = "Cuadrícula métrica 25 cm", IsChecked = true };
    private readonly TextBlock _viewInfo = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _levelPicker = new() { MinWidth = 170 };
    private Guid? _activeLevelId;

    public bool HasUnsavedChanges { get; private set; }
    public void MarkSaved() => HasUnsavedChanges = false;
    private readonly StackPanel _sidebar = new() { Spacing = 10, Margin = new Thickness(8) };

    public event Action? ModelChanged;
    public event Action<string>? Message;

    public PlanWorkspace()
    {
        ColumnDefinitions = new ColumnDefinitions("245,*");
        Background = Brushes.WhiteSmoke;
        _sidebar.Children.Add(new TextBlock { Text = "NAVEGADOR BIM", FontWeight = FontWeight.Bold });
        _browser.Height = 230;
        _sidebar.Children.Add(_browser);
        _sidebar.Children.Add(new TextBlock { Text = "NIVEL ACTIVO", FontWeight = FontWeight.Bold });
        _sidebar.Children.Add(_levelPicker);
        _sidebar.Children.Add(_viewInfo);
        _sidebar.Children.Add(new TextBlock { Text = "REFERENCIAS DE DIBUJO", FontWeight = FontWeight.Bold });
        _sidebar.Children.Add(_snapEndpoints);
        _sidebar.Children.Add(_snapGrid);
        _sidebar.Children.Add(new TextBlock { Text = "Mantén Shift al dibujar para bloquear horizontal o vertical.",
            TextWrapping = TextWrapping.Wrap });
        // Avalonia CheckBox exposes IsChecked as a styled property, not WPF Checked/Unchecked events.
        _snapEndpoints.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsChecked") Redraw();
        };
        _snapGrid.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsChecked") Redraw();
        };
        _levelPicker.SelectionChanged += (_, _) =>
        {
            if (_levelPicker.SelectedItem is LevelEntry level)
            {
                _activeLevelId = level.Id;
                _selectedId = null;
                ShowSelected();
                FitToModel();
            }
        };
        _sidebar.Children.Add(new TextBlock { Text = "PROPIEDADES", FontWeight = FontWeight.Bold });
        _sidebar.Children.Add(_inspector);
        _sidebar.Children.Add(_axisName);
        _sidebar.Children.Add(_rename);
        _sidebar.Children.Add(_delete);
        _sidebar.Children.Add(new TextBlock { Text = "HISTORIAL", FontWeight = FontWeight.Bold });
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        history.Children.Add(_undo);
        history.Children.Add(_redo);
        _sidebar.Children.Add(history);
        _sidebar.Children.Add(_hint);

        var pane = new ScrollViewer { Content = _sidebar, Background = Brushes.WhiteSmoke };
        SetColumn(pane, 0);
        Children.Add(pane);
        SetColumn(_canvas, 1);
        Children.Add(_canvas);

        _canvas.PointerPressed += CanvasPressed;
        _canvas.PointerMoved += CanvasMoved;
        _canvas.PointerReleased += CanvasReleased;
        _canvas.PointerWheelChanged += CanvasWheel;
        _canvas.PointerCaptureLost += (_, _) => _panPointer = null;
        _canvas.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { _firstPoint = null; SetTool(EditTool.Select); e.Handled = true; }
        };
        _canvas.Focusable = true;
        _canvas.SizeChanged += (_, _) => Redraw();
        _browser.SelectionChanged += (_, _) =>
        {
            if (_browser.SelectedItem is BrowserEntry entry)
            {
                _selectedId = entry.Id;
                ShowSelected();
                Redraw();
            }
        };
        _rename.Click += (_, _) => RenameAxis();
        _delete.Click += async (_, _) => await DeleteSelectedAsync();
        _undo.Click += (_, _) => Undo();
        _redo.Click += (_, _) => Redo();
        SetTool(EditTool.Select);
    }

    public DocumentSession? Session => _session;

    public void Undo()
    {
        if (_session?.CanUndo != true) return;
        _session.Undo();
        Changed(true);
    }

    public void Redo()
    {
        if (_session?.CanRedo != true) return;
        _session.Redo();
        Changed(true);
    }

    public void LoadDocument(BuildingDocument? document)
    {
        _session = document is null ? null : new DocumentSession(document);
        _selectedId = null;
        _firstPoint = null;
        HasUnsavedChanges = false;
        _activeLevelId = _session?.Document.Levels.FirstOrDefault()?.Id;
        _levelPicker.ItemsSource = _session?.Document.Levels
            .Select(level => new LevelEntry(level.Id, level.Name, level.Elevation)).ToArray();
        _levelPicker.SelectedItem = (_levelPicker.ItemsSource as LevelEntry[])?
            .FirstOrDefault(level => level.Id == _activeLevelId);
        FitToModel();
        Changed();
    }

    public void SetTool(EditTool tool)
    {
        _tool = tool;
        _firstPoint = null;
        _previewWorldPoint = null;
        _previewSnapKind = PlanSnapping.SnapKind.None;
        _hint.Text = tool switch
        {
            EditTool.Wall => "Muro: elige punto inicial y final. Escala en metros; muros de 20 cm y 3 m de alto.",
            EditTool.GridAxis => "Eje: elige dos puntos. El nombre se genera automáticamente sin duplicados.",
            _ => "Seleccionar: haz clic sobre un muro o eje para consultar sus propiedades."
        };
        Redraw();
    }

    public void FitToModel()
    {
        if (_session is null) { _scale = 43; _offset = new Point(0, 0); return; }
        var points = _session.Document.Walls.Where(w => w.LevelId == _activeLevelId).SelectMany(w => new[] { w.Start, w.End })
            .Concat(_session.Document.Grids.SelectMany(g => new[] { g.Start, g.End })).ToArray();
        if (points.Length == 0) { _scale = 43; _offset = new Point(0, 0); return; }
        var minX = points.Min(p => p.X); var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y); var maxY = points.Max(p => p.Y);
        var width = Math.Max(1, _canvas.Bounds.Width - 100);
        var height = Math.Max(1, _canvas.Bounds.Height - 100);
        _scale = Math.Clamp(Math.Min(width / Math.Max(3, maxX - minX), height / Math.Max(3, maxY - minY)), 10, 130);
        _offset = new Point(-(minX + maxX) / 2 * _scale, (minY + maxY) / 2 * _scale);
        Redraw();
    }

    private Point ToScreen(Point2 p) => new(
        _canvas.Bounds.Width / 2 + _offset.X + p.X * _scale,
        _canvas.Bounds.Height / 2 + _offset.Y - p.Y * _scale);

    private Point2 ToWorld(Point p) => new(
        Math.Round((p.X - _canvas.Bounds.Width / 2 - _offset.X) / _scale, 2),
        Math.Round((_canvas.Bounds.Height / 2 + _offset.Y - p.Y) / _scale, 2));

    private void CanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_session is null) { Message?.Invoke("Crea o abre un proyecto primero."); return; }
        var properties = e.GetCurrentPoint(_canvas).Properties;
        if (properties.IsMiddleButtonPressed || properties.IsRightButtonPressed)
        {
            _panPointer = e.GetPosition(_canvas);
            e.Pointer.Capture(_canvas);
            e.Handled = true;
            return;
        }
        if (!properties.IsLeftButtonPressed) return;
        _canvas.Focus();
        var point = e.GetPosition(_canvas);
        if (_tool == EditTool.Select)
        {
            _selectedId = HitTestGeometry(point);
            ShowSelected();
            Redraw();
            return;
        }
        var snap = ResolveSnap(point, e.KeyModifiers);
        var modelPoint = snap.Point;
        if (_firstPoint is null)
        {
            _firstPoint = modelPoint;
            _previewWorldPoint = modelPoint;
            _previewSnapKind = snap.Kind;
            Message?.Invoke("Primer punto registrado. Selecciona el segundo punto.");
            return;
        }
        var first = _firstPoint.Value;
        _firstPoint = null;
        _previewWorldPoint = null;
        _previewSnapKind = PlanSnapping.SnapKind.None;
        if (first.DistanceTo(modelPoint) < 0.05)
        {
            Message?.Invoke("Los puntos deben estar separados al menos 5 cm.");
            return;
        }
        try
        {
            if (_tool == EditTool.Wall)
            {
                _selectedId = _session.AddWall(_activeLevelId ?? _session.Document.Levels[0].Id, first, modelPoint);
                Message?.Invoke("Muro creado en el modelo BIM.");
            }
            else
            {
                var next = 1;
                while (_session.Document.Grids.Any(g =>
                    string.Equals(g.Name, next.ToString(), StringComparison.OrdinalIgnoreCase))) next++;
                _selectedId = _session.AddGridAxis(next.ToString(), first, modelPoint);
                Message?.Invoke("Eje creado en el modelo BIM.");
            }
            Changed(true);
        }
        catch (InvalidDataException ex) { Message?.Invoke(ex.Message); }
    }

    private PlanSnapping.Result ResolveSnap(Point pixelPosition, KeyModifiers modifiers)
    {
        if (_session is null) return new PlanSnapping.Result(ToWorld(pixelPosition), PlanSnapping.SnapKind.None);
        return PlanSnapping.Resolve(_session.Document, _activeLevelId, ToWorld(pixelPosition), _scale,
            _snapGrid.IsChecked == true, _snapEndpoints.IsChecked == true, _firstPoint,
            (modifiers & KeyModifiers.Shift) != 0);
    }

    private void CanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        var point = e.GetPosition(_canvas);
        var before = ToWorld(point);
        _scale = Math.Clamp(_scale * Math.Pow(1.12, e.Delta.Y), 6, 450);
        _offset = new Point(
            point.X - _canvas.Bounds.Width / 2 - before.X * _scale,
            point.Y - _canvas.Bounds.Height / 2 + before.Y * _scale);
        _viewInfo.Text = $"Escala: {_scale:0.#} píxeles/m · Rueda: zoom · botón central: desplazar";
        Redraw();
        e.Handled = true;
    }

    private void CanvasMoved(object? sender, PointerEventArgs e)
    {
        if (_panPointer is not Point last)
        {
            if (_firstPoint is not null && _tool != EditTool.Select)
            {
                var snap = ResolveSnap(e.GetPosition(_canvas), e.KeyModifiers);
                _previewWorldPoint = snap.Point;
                _previewSnapKind = snap.Kind;
                Redraw();
            }
            return;
        }
        var current = e.GetPosition(_canvas);
        _offset = new Point(_offset.X + current.X - last.X, _offset.Y + current.Y - last.Y);
        _panPointer = current;
        Redraw();
        e.Handled = true;
    }

    private void CanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panPointer is null) return;
        _panPointer = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private Guid? HitTestGeometry(Point pointer)
    {
        if (_session is null) return null;
        var bestDistance = 11.0;
        Guid? best = null;
        foreach (var wall in _session.Document.Walls.Where(w => w.LevelId == _activeLevelId))
        {
            var d = DistanceFromSegment(pointer, ToScreen(wall.Start), ToScreen(wall.End));
            if (d < bestDistance) { best = wall.Id; bestDistance = d; }
        }
        foreach (var axis in _session.Document.Grids)
        {
            var d = DistanceFromSegment(pointer, ToScreen(axis.Start), ToScreen(axis.End));
            if (d < bestDistance) { best = axis.Id; bestDistance = d; }
        }
        return best;
    }

    private static double DistanceFromSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var denom = dx * dx + dy * dy;
        var t = denom <= 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / denom, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
    }

    private void RenameAxis()
    {
        if (_session is null || _selectedId is not Guid id) return;
        var axis = _session.Document.Grids.FirstOrDefault(g => g.Id == id);
        if (axis is null) { Message?.Invoke("Selecciona un eje para renombrar."); return; }
        var name = (_axisName.Text ?? "").Trim();
        if (name.Length is < 1 or > 32)
        {
            Message?.Invoke("El nombre del eje debe contener entre 1 y 32 caracteres.");
            return;
        }
        var conflict = _session.Document.Grids.FirstOrDefault(g => g.Id != id &&
            string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (conflict is not null)
        {
            _selectedId = conflict.Id;
            ShowSelected();
            Message?.Invoke($"Ya existe un eje llamado '{name}'. Lo he seleccionado para que puedas localizarlo.");
            Redraw();
            return;
        }
        try
        {
            _session.UpdateGridAxis(id, name, axis.Start, axis.End);
            Changed(true);
        }
        catch (InvalidDataException ex) { Message?.Invoke(ex.Message); }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_session is null || _selectedId is not Guid id) return;
        var axis = _session.Document.Grids.FirstOrDefault(g => g.Id == id);
        var wall = _session.Document.Walls.FirstOrDefault(w => w.Id == id);
        if (axis is null && wall is null) return;

        var details = axis is not null
            ? $"¿Eliminar el eje '{axis.Name}'? Los muros, puertas y ventanas permanecerán intactos."
            : "¿Eliminar el muro seleccionado? Los ejes permanecerán intactos. Las puertas o ventanas alojadas en este muro pueden eliminarse con su anfitrión.";
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var dialog = new Window
        {
            Title = "Confirmar eliminación BIM",
            Width = 440,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var content = new StackPanel { Spacing = 20, Margin = new Thickness(20) };
        content.Children.Add(new TextBlock { Text = details, TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal,
            Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90 };
        var confirm = new Button { Content = axis is not null ? "Eliminar solo eje" : "Eliminar muro", MinWidth = 120 };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        content.Children.Add(actions);
        dialog.Content = content;
        if (!await dialog.ShowDialog<bool>(owner)) return;

        // Reselecting a different element while the dialog is open must never alter the deletion target.
        if (axis is not null && _session.Document.Grids.Any(g => g.Id == id))
        {
            _session.RemoveGridAxis(id);
            Message?.Invoke("Eje eliminado; no se han eliminado muros.");
        }
        else if (wall is not null && _session.Document.Walls.Any(w => w.Id == id))
        {
            _session.RemoveWall(id);
            Message?.Invoke("Muro eliminado; no se han eliminado ejes.");
        }
        _selectedId = null;
        Changed(true);
    }

    private void ShowSelected()
    {
        var axis = _session?.Document.Grids.FirstOrDefault(g => g.Id == _selectedId);
        var wall = _session?.Document.Walls.FirstOrDefault(w => w.Id == _selectedId);
        _inspector.Text = axis is not null
            ? $"Eje {axis.Name}\nInicio: {axis.Start.X:0.00}, {axis.Start.Y:0.00} m\nFin: {axis.End.X:0.00}, {axis.End.Y:0.00} m"
            : wall is not null
                ? $"Muro {wall.Id.ToString()[..8]}\nLongitud: {wall.Length:0.00} m\nEspesor: {wall.Thickness:0.00} m\nAltura: {wall.Height:0.00} m"
                : "Selecciona un muro o eje en el plano.";
        _axisName.Text = axis?.Name ?? "";
        _rename.IsEnabled = axis is not null;
        _delete.IsEnabled = axis is not null || wall is not null;
    }

    private void Changed(bool modified = false)
    {
        if (modified) HasUnsavedChanges = true;
        _viewInfo.Text = $"Escala: {_scale:0.#} píxeles/m · Rueda: zoom · botón central: desplazar";
        _undo.IsEnabled = _session?.CanUndo == true;
        _redo.IsEnabled = _session?.CanRedo == true;
        var entries = _session is null ? Array.Empty<BrowserEntry>() :
            _session.Document.Grids.Select(g => new BrowserEntry(g.Id, "Eje " + g.Name))
                .Concat(_session.Document.Walls.Where(w => w.LevelId == _activeLevelId).Select(w =>
                    new BrowserEntry(w.Id, $"Muro {w.Length:0.00} m")))
                .ToArray();
        _browser.ItemsSource = entries;
        ShowSelected();
        Redraw();
        ModelChanged?.Invoke();
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        var bounds = _canvas.Bounds;
        if (bounds.Width < 1 || bounds.Height < 1) return;
        var spacing = _scale;
        for (var x = bounds.Width / 2 + _offset.X % spacing; x < bounds.Width; x += spacing)
            DrawSegment(new Point(x, 0), new Point(x, bounds.Height), Brushes.Gainsboro, 0.7);
        for (var x = bounds.Width / 2 + _offset.X % spacing - spacing; x >= 0; x -= spacing)
            DrawSegment(new Point(x, 0), new Point(x, bounds.Height), Brushes.Gainsboro, 0.7);
        for (var y = bounds.Height / 2 + _offset.Y % spacing; y < bounds.Height; y += spacing)
            DrawSegment(new Point(0, y), new Point(bounds.Width, y), Brushes.Gainsboro, 0.7);
        for (var y = bounds.Height / 2 + _offset.Y % spacing - spacing; y >= 0; y -= spacing)
            DrawSegment(new Point(0, y), new Point(bounds.Width, y), Brushes.Gainsboro, 0.7);

        if (_session is null) return;
        foreach (var wall in _session.Document.Walls.Where(w => w.LevelId == _activeLevelId))
            DrawSegment(ToScreen(wall.Start), ToScreen(wall.End),
                wall.Id == _selectedId ? Brushes.DodgerBlue : Brushes.DarkSlateGray,
                Math.Max(3, wall.Thickness * _scale));
        foreach (var axis in _session.Document.Grids)
        {
            var start = ToScreen(axis.Start); var end = ToScreen(axis.End);
            DrawSegment(start, end, axis.Id == _selectedId ? Brushes.RoyalBlue : Brushes.DarkOrange, 1.5);
            var bubble = new Border
            {
                Width = 27, Height = 27, CornerRadius = new CornerRadius(14),
                Background = Brushes.White, BorderBrush = Brushes.DarkOrange, BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = axis.Name, TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }
            };
            Canvas.SetLeft(bubble, start.X - 13);
            Canvas.SetTop(bubble, start.Y - 13);
            _canvas.Children.Add(bubble);
        }
        if (_firstPoint is Point2 from && _previewWorldPoint is Point2 to)
        {
            var first = ToScreen(from);
            var last = ToScreen(to);
            DrawSegment(first, last, Brushes.DodgerBlue,
                _tool == EditTool.Wall ? Math.Max(3, 0.20 * _scale) : 1.5);
            if (_previewSnapKind != PlanSnapping.SnapKind.None)
            {
                var marker = new Ellipse
                {
                    Width = 12, Height = 12, Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2, Fill = Brushes.White,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(marker, last.X - 6);
                Canvas.SetTop(marker, last.Y - 6);
                _canvas.Children.Add(marker);
            }
        }
    }

    private void DrawSegment(Point a, Point b, IBrush color, double thickness)
    {
        var segment = new Line
        {
            StartPoint = a,
            EndPoint = b,
            Stroke = color,
            StrokeThickness = thickness,
            IsHitTestVisible = false
        };
        _canvas.Children.Add(segment);
    }

    private sealed record LevelEntry(Guid Id, string Name, double Elevation)
    {
        public override string ToString() => $"{Name} ({Elevation:0.##} m)";
    }

    private sealed record BrowserEntry(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
