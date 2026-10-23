using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Guanacaste3D.Core;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop;

public partial class MainWindow : Window
{
    private DocumentSession _session;
    private Guid _activeLevelId;
    private Guid? _selectedWallId;
    private double? _selectedWallOffset;
    private Point2? _pendingWallStart;
    private string? _filePath;
    private bool _dirty;
    private bool _drawingWall;
    private double _scale = 36;
    private bool _refreshingLevels;

    public MainWindow()
    {
        InitializeComponent();
        _session = new DocumentSession(BuildingDocument.Create());
        _activeLevelId = _session.Document.Levels[0].Id;
        _session.Changed += OnDocumentChanged;
        RefreshAll();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        _dirty = true;
        RefreshAll();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        AttachDocument(BuildingDocument.Create(), null);
        StatusText.Text = "Proyecto nuevo.";
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Proyecto Guanacaste 3D (*.g3d.json)|*.g3d.json|JSON (*.json)|*.json",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var document = ProjectFile.Load(dialog.FileName);
            AttachDocument(document, dialog.FileName);
            StatusText.Text = "Proyecto abierto correctamente.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(this, ex.Message, "No se pudo abrir", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveProject(false);
    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveProject(true);

    private bool SaveProject(bool choosePath)
    {
        var path = _filePath;
        if (choosePath || path is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Proyecto Guanacaste 3D (*.g3d.json)|*.g3d.json",
                FileName = _session.Document.Name + ".g3d.json",
                AddExtension = true,
                DefaultExt = ".g3d.json"
            };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }
        try
        {
            ProjectFile.Save(path!, _session.Document);
            _filePath = path;
            _dirty = false;
            StatusText.Text = "Proyecto guardado: " + System.IO.Path.GetFileName(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "No se pudo guardar", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void AttachDocument(BuildingDocument document, string? filename)
    {
        _session.Changed -= OnDocumentChanged;
        _session = new DocumentSession(document);
        _session.Changed += OnDocumentChanged;
        _activeLevelId = document.Levels[0].Id;
        _pendingWallStart = null;
        _selectedWallId = null;
        _selectedWallOffset = null;
        _filePath = filename;
        _dirty = false;
        _drawingWall = false;
        RefreshAll();
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        return MessageBox.Show(this,
            "Hay cambios sin guardar. ¿Deseas continuar y descartarlos?",
            "Guanacaste 3D", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmDiscard()) e.Cancel = true;
        base.OnClosing(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Undo_Click(object sender, RoutedEventArgs e) { _session.Undo(); _selectedWallId = null; RefreshAll(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { _session.Redo(); _selectedWallId = null; RefreshAll(); }
    private void Select_Click(object sender, RoutedEventArgs e) => SetWallTool(false);
    private void Wall_Click(object sender, RoutedEventArgs e) => SetWallTool(true);
    private void Door_Click(object sender, RoutedEventArgs e) =>
        CreateHostedOpening(OpeningKind.Door);
    private void Window_Click(object sender, RoutedEventArgs e) =>
        CreateHostedOpening(OpeningKind.Window);

    private void CreateHostedOpening(OpeningKind kind)
    {
        if (_selectedWallId is not Guid wallId)
        {
            StatusText.Text = "Selecciona primero un muro en planta.";
            return;
        }
        var wall = _session.Document.Walls.FirstOrDefault(w => w.Id == wallId);
        if (wall is null) return;
        try
        {
            if (kind == OpeningKind.Door)
                _session.AddOpening(wallId, OpeningKind.Door,
                    offset: _selectedWallOffset, width: 0.90, height: 2.10);
            else
                _session.AddOpening(wallId, OpeningKind.Window,
                    offset: _selectedWallOffset, width: 1.00, sill: 0.90, height: 1.20);
            StatusText.Text = kind == OpeningKind.Door ? "Hueco de puerta creado." : "Hueco de ventana creado.";
            RefreshAll();
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(this, ex.Message, "Abertura no válida",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    private void ShowPlan_Click(object sender, RoutedEventArgs e) => ViewTabs.SelectedIndex = 0;
    private void ShowModel_Click(object sender, RoutedEventArgs e) => ViewTabs.SelectedIndex = 1;
    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Guanacaste 3D 0.1.0\nPrototipo BIM de escritorio en C# y .NET 10.",
            "Acerca de Guanacaste 3D", MessageBoxButton.OK, MessageBoxImage.Information);

    private void SetWallTool(bool drawing)
    {
        _drawingWall = drawing;
        _pendingWallStart = null;
        StatusText.Text = drawing ? "Muro: haz clic en inicio y fin (Esc cancela)." : "Modo selección.";
        RenderPlan();
    }

    private void AddLevel_Click(object sender, RoutedEventArgs e)
    {
        _activeLevelId = _session.AddLevel();
        RefreshAll();
    }

    private void LevelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingLevels || LevelPicker.SelectedItem is not ComboBoxItem item || item.Tag is not Guid id)
            return;
        _activeLevelId = id;
        _selectedWallId = null;
        _pendingWallStart = null;
        RefreshAll();
    }

    private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || item.Tag is not Guid id) return;
        if (_session.Document.Walls.Any(w => w.Id == id))
        {
            _selectedWallId = id;
            RefreshProperties();
            RenderPlan();
        }
        else if (_session.Document.Levels.Any(l => l.Id == id))
        {
            _activeLevelId = id;
            _selectedWallId = null;
            RefreshLevelPicker();
            RenderPlan();
            RefreshProperties();
        }
    }

    private void PlanCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_drawingWall)
        {
            _selectedWallId = (e.OriginalSource as FrameworkElement)?.Tag is Guid id
                && _session.Document.Walls.Any(w => w.Id == id) ? id : null;
            _selectedWallOffset = null;
            if (_selectedWallId is Guid wallId)
            {
                var wall = _session.Document.Walls.First(w => w.Id == wallId);
                var clicked = CanvasToWorld(e.GetPosition(PlanCanvas));
                var vx = (wall.End.X - wall.Start.X) / wall.Length;
                var vy = (wall.End.Y - wall.Start.Y) / wall.Length;
                _selectedWallOffset = Math.Clamp(
                    (clicked.X - wall.Start.X) * vx + (clicked.Y - wall.Start.Y) * vy,
                    0, wall.Length);
            }
            RefreshProperties();
            RenderPlan();
            return;
        }
        var point = CanvasToWorld(e.GetPosition(PlanCanvas));
        if (_pendingWallStart is null)
        {
            _pendingWallStart = point;
            StatusText.Text = "Elige el segundo extremo del muro.";
            RenderPlan();
            return;
        }
        try
        {
            _selectedWallId = _session.AddWall(_activeLevelId, _pendingWallStart.Value, point);
            _pendingWallStart = null;
            StatusText.Text = "Muro creado. Haz clic para iniciar otro.";
            RefreshAll();
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(this, ex.Message, "Geometría no válida", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PlanCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingWallStart = null;
        RenderPlan();
        StatusText.Text = "Trazo cancelado.";
    }

    private void PlanCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _scale = Math.Clamp(_scale * (e.Delta > 0 ? 1.15 : 1.0 / 1.15), 8, 200);
        RenderPlan();
    }

    private void PlanCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderPlan();
    private Point WorldToCanvas(Point2 point) =>
        new(PlanCanvas.ActualWidth / 2 + point.X * _scale,
            PlanCanvas.ActualHeight / 2 - point.Y * _scale);
    private Point2 CanvasToWorld(Point point) =>
        new((point.X - PlanCanvas.ActualWidth / 2) / _scale,
            (PlanCanvas.ActualHeight / 2 - point.Y) / _scale);

    private void RenderPlan()
    {
        if (PlanCanvas is null || _session is null) return;
        PlanCanvas.Children.Clear();
        if (PlanCanvas.ActualWidth < 1 || PlanCanvas.ActualHeight < 1) return;
        var left = -PlanCanvas.ActualWidth / 2;
        var right = PlanCanvas.ActualWidth / 2;
        var top = -PlanCanvas.ActualHeight / 2;
        var bottom = PlanCanvas.ActualHeight / 2;
        for (var gx = Math.Ceiling(left / _scale); gx <= right / _scale; gx++)
            AddLine(new Point2(gx, -bottom / _scale), new Point2(gx, -top / _scale), 0.5, Brushes.Gainsboro);
        for (var gy = Math.Ceiling(top / _scale); gy <= bottom / _scale; gy++)
            AddLine(new Point2(left / _scale, -gy), new Point2(right / _scale, -gy), 0.5, Brushes.Gainsboro);
        AddLine(new Point2(left / _scale, 0), new Point2(right / _scale, 0), 1, Brushes.LightSteelBlue);
        AddLine(new Point2(0, -bottom / _scale), new Point2(0, -top / _scale), 1, Brushes.LightSteelBlue);
        foreach (var wall in _session.Document.Walls.Where(w => w.LevelId == _activeLevelId))
        {
            var color = wall.Id == _selectedWallId ? Brushes.DarkOrange : Brushes.DarkSlateGray;
            AddLine(wall.Start, wall.End, Math.Max(3, wall.Thickness * _scale), color, wall.Id);
            foreach (var opening in _session.Document.Openings.Where(o => o.WallId == wall.Id))
            {
                var dx = (wall.End.X - wall.Start.X) / wall.Length;
                var dy = (wall.End.Y - wall.Start.Y) / wall.Length;
                Point2 Along(double offset) => new(
                    wall.Start.X + dx * offset, wall.Start.Y + dy * offset);
                var from = Along(opening.Begin);
                var to = Along(opening.End);
                AddLine(from, to, Math.Max(4, wall.Thickness * _scale + 2),
                    Brushes.White, wall.Id);
                if (opening.Kind == OpeningKind.Window)
                    AddLine(from, to, 2, Brushes.SteelBlue, wall.Id);
            }
        }
        if (_pendingWallStart is Point2 start)
        {
            var p = WorldToCanvas(start);
            var marker = new Ellipse { Width = 12, Height = 12, Fill = Brushes.ForestGreen };
            Canvas.SetLeft(marker, p.X - 6);
            Canvas.SetTop(marker, p.Y - 6);
            PlanCanvas.Children.Add(marker);
        }
    }

    private void AddLine(Point2 start, Point2 end, double thickness, Brush color, Guid? tag = null)
    {
        var p = WorldToCanvas(start);
        var q = WorldToCanvas(end);
        var line = new Line
        {
            X1 = p.X, Y1 = p.Y, X2 = q.X, Y2 = q.Y,
            Stroke = color, StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Square,
            StrokeEndLineCap = PenLineCap.Square,
            SnapsToDevicePixels = true,
            Tag = tag
        };
        PlanCanvas.Children.Add(line);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id) { StatusText.Text = "Selecciona un muro."; return; }
        _selectedWallId = _session.DuplicateWall(id, 1, 1);
        _selectedWallOffset = null;
        StatusText.Text = "Muro duplicado a 1 metro de distancia en X e Y.";
        RefreshAll();
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id)
        {
            StatusText.Text = "Selecciona un muro para rotarlo.";
            return;
        }
        try
        {
            _session.RotateWall(id, 90);
            _selectedWallOffset = null;
            StatusText.Text = "Muro rotado 90° alrededor de su punto inicial.";
            RefreshAll();
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(this, ex.Message, "Rotar muro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id) { StatusText.Text = "Selecciona un muro."; return; }
        _session.MoveWall(id, 1, 0);
        _selectedWallOffset = null;
        StatusText.Text = "Muro desplazado 1 metro hacia X positivo.";
        RefreshAll();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id) return;
        _session.RemoveWall(id);
        _selectedWallId = null;
        RefreshAll();
    }

    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWallId is not Guid id)
        {
            StatusText.Text = "Selecciona un muro para modificarlo.";
            return;
        }
        if (!double.TryParse(ThicknessInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var thickness) ||
            !double.TryParse(HeightInput.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var height))
        {
            MessageBox.Show(this, "Usa números válidos en las dimensiones.", "Parámetros", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try { _session.UpdateWall(id, thickness, height); }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(this, ex.Message, "Parámetros inválidos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshAll()
    {
        if (_session is null) return;
        Title = "Guanacaste 3D — " + _session.Document.Name;
        if (!_session.Document.Levels.Any(l => l.Id == _activeLevelId))
            _activeLevelId = _session.Document.Levels[0].Id;
        if (!_session.Document.Walls.Any(w => w.Id == _selectedWallId)) _selectedWallId = null;
        RefreshLevelPicker();
        RefreshTree();
        RefreshProperties();
        RenderPlan();
        Render3D();
    }

    private void RefreshLevelPicker()
    {
        _refreshingLevels = true;
        LevelPicker.Items.Clear();
        foreach (var level in _session.Document.Levels)
        {
            var item = new ComboBoxItem { Content = level.Name, Tag = level.Id };
            LevelPicker.Items.Add(item);
            if (level.Id == _activeLevelId) LevelPicker.SelectedItem = item;
        }
        _refreshingLevels = false;
    }

    private void RefreshTree()
    {
        ProjectTree.Items.Clear();
        var project = new TreeViewItem { Header = _session.Document.Name, IsExpanded = true };
        var levels = new TreeViewItem { Header = "Plantas", IsExpanded = true };
        foreach (var level in _session.Document.Levels)
        {
            var branch = new TreeViewItem { Header = $"{level.Name}  ({level.Elevation:0.##} m)", Tag = level.Id, IsExpanded = true };
            foreach (var wall in _session.Document.Walls.Where(w => w.LevelId == level.Id))
                {
                    var wallBranch = new TreeViewItem { Header = $"Muro · {wall.Length:0.00} m", Tag = wall.Id };
                    foreach (var opening in _session.Document.Openings.Where(o => o.WallId == wall.Id))
                        wallBranch.Items.Add(new TreeViewItem
                        {
                            Header = $"{(opening.Kind == OpeningKind.Door ? "Puerta" : "Ventana")} · {opening.Width:0.00} m",
                            Tag = wall.Id
                        });
                    branch.Items.Add(wallBranch);
                }
            levels.Items.Add(branch);
        }
        project.Items.Add(levels);
        ProjectTree.Items.Add(project);
    }

    private void RefreshProperties()
    {
        var wall = _session.Document.Walls.FirstOrDefault(w => w.Id == _selectedWallId);
        if (wall is null)
        {
            PropertiesText.Text = $"Proyecto: {_session.Document.Name}\n" +
                $"Niveles: {_session.Document.Levels.Count}\n" +
                $"Muros: {_session.Document.Walls.Count}\n\n" +
                "Selecciona un muro para ver sus propiedades.";
            return;
        }
        PropertiesText.Text = $"Elemento: muro\nLongitud: {wall.Length:0.00} m\n" +
            $"Espesor: {wall.Thickness:0.00} m\nAltura: {wall.Height:0.00} m\n" +
            $"Aberturas: {_session.Document.Openings.Count(o => o.WallId == wall.Id)}\n" +
            $"Volumen neto: {WallGeometry.NetVolume(wall, _session.Document.Openings.Where(o => o.WallId == wall.Id)):0.00} m³\n" +
            $"Identificador: {wall.Id.ToString()[..8]}";
        ThicknessInput.Text = wall.Thickness.ToString("0.00", CultureInfo.CurrentCulture);
        HeightInput.Text = wall.Height.ToString("0.00", CultureInfo.CurrentCulture);
    }

    private void Render3D()
    {
        if (ModelViewport is null || _session is null) return;
        ModelViewport.Children.Clear();
        ModelViewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(Color.FromRgb(178, 193, 184)) });
        ModelViewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Colors.White, new Vector3D(-2, -6, -3)) });
        AddGround();
        foreach (var wall in _session.Document.Walls)
        {
            var elevation = _session.Document.Levels.First(l => l.Id == wall.LevelId).Elevation;
            var length = wall.Length;
            var offsetX = -(wall.End.Y - wall.Start.Y) * wall.Thickness / (2 * length);
            var offsetY = (wall.End.X - wall.Start.X) * wall.Thickness / (2 * length);
            Point2 Along(double position) => new(
                wall.Start.X + position * (wall.End.X - wall.Start.X) / length,
                wall.Start.Y + position * (wall.End.Y - wall.Start.Y) / length);
            Point3D V(Point2 p, int side, double z) =>
                new(p.X + side * offsetX, elevation + z, -(p.Y + side * offsetY));
            var hosted = _session.Document.Openings.Where(o => o.WallId == wall.Id);
            foreach (var solid in WallGeometry.Solids(wall, hosted))
            {
                var start = Along(solid.From);
                var end = Along(solid.To);
                var corners = new[]
                {
                    V(start, 1, solid.Bottom), V(end, 1, solid.Bottom),
                    V(end, -1, solid.Bottom), V(start, -1, solid.Bottom),
                    V(start, 1, solid.Top), V(end, 1, solid.Top),
                    V(end, -1, solid.Top), V(start, -1, solid.Top)
                };
                var mesh = new MeshGeometry3D();
                int[][] faces =
                [
                    [0, 1, 5, 4], [2, 3, 7, 6], [1, 2, 6, 5],
                    [3, 0, 4, 7], [4, 5, 6, 7], [3, 2, 1, 0]
                ];
                foreach (var face in faces)
                {
                    var i = mesh.Positions.Count;
                    foreach (var vertex in face) mesh.Positions.Add(corners[vertex]);
                    foreach (var index in new[] { 0, 1, 2, 0, 2, 3 })
                        mesh.TriangleIndices.Add(i + index);
                }
                var material = new DiffuseMaterial(
                    new SolidColorBrush(Color.FromRgb(185, 203, 192)));
                ModelViewport.Children.Add(new ModelVisual3D
                {
                    Content = new GeometryModel3D(mesh, material) { BackMaterial = material }
                });
            }
        }
        var allPoints = _session.Document.Walls.SelectMany(w => new[] { w.Start, w.End }).ToArray();
        var centerX = allPoints.Length == 0 ? 0 : (allPoints.Min(p => p.X) + allPoints.Max(p => p.X)) / 2;
        var centerY = allPoints.Length == 0 ? 0 : (allPoints.Min(p => p.Y) + allPoints.Max(p => p.Y)) / 2;
        var range = allPoints.Length == 0 ? 10 :
            Math.Max(allPoints.Max(p => p.X) - allPoints.Min(p => p.X),
                     allPoints.Max(p => p.Y) - allPoints.Min(p => p.Y));
        var distance = Math.Max(12, range * 1.7);
        var cameraPos = new Point3D(centerX + distance, distance * 0.8, -centerY + distance);
        var target = new Point3D(centerX, 1, -centerY);
        ModelViewport.Camera = new PerspectiveCamera(cameraPos, target - cameraPos, new Vector3D(0, 1, 0), 50);
    }

    private void AddGround()
    {
        var positions = new Point3DCollection
        {
            new(-150, -0.02, -150), new(150, -0.02, -150),
            new(150, -0.02, 150), new(-150, -0.02, 150)
        };
        var mesh = new MeshGeometry3D
        {
            Positions = positions,
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        var brush = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(238, 243, 240)));
        ModelViewport.Children.Add(new ModelVisual3D { Content = new GeometryModel3D(mesh, brush) { BackMaterial = brush } });
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) { base.OnPreviewKeyDown(e); return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { SaveProject(false); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { _session.Undo(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { _session.Redo(); e.Handled = true; }
        else if (e.Key == Key.W) { SetWallTool(true); e.Handled = true; }
        else if (e.Key == Key.Escape) { SetWallTool(false); e.Handled = true; }
        else if (e.Key == Key.Delete) { Delete_Click(this, new RoutedEventArgs()); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
}
