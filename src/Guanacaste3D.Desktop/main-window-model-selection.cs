using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Selecting in the 3D view: a click picks the element under the cursor and selects it exactly as the plan does, so the
/// properties, the plan and the Delete key all follow. The selected element is outlined in the model and measured with
/// temporary dimensions, whichever view the selection was made in.
/// </summary>
public partial class MainWindow
{
    private ElementPicker? _picker;
    private (PickedKind Kind, Guid Id)? _shownSelection;
    private readonly List<ModelVisual3D> _selectionVisuals = [];
    private readonly List<(Border Chip, Vector3 Middle)> _dimensionChips = [];

    /// <summary>
    /// Where the plan canvas should read the pointer from while a click made in the 3D view or the render preview is being
    /// replayed into the plan's own handlers; null means the real mouse.
    /// </summary>
    private Point? _planPointerOverride;

    private Point PlanPosition(MouseEventArgs e) => _planPointerOverride ?? e.GetPosition(PlanCanvas);

    /// <summary>Wires clicks in the 3D view and the render preview to selection and to the armed tool.</summary>
    private void InitializeModelSelection()
    {
        if (_modelNavigator is null) return;
        _modelNavigator.Clicked += position => HandleViewClick(position, ModelSurface);
        _modelNavigator.DoubleClicked += _ => OpenSelectionEditor();
        if (RenderPanelControl.Navigator is { } renderNavigator)
        {
            renderNavigator.Clicked += position => HandleViewClick(position, RenderPanelControl.Surface);
            renderNavigator.DoubleClicked += _ => OpenSelectionEditor();
        }
    }

    /// <summary>
    /// What a double-click opens, in any view: a wall, its type; a door or window, its family with an editable plan and 3D
    /// view; any other element, its property editor. The first click of the double-click has already selected the element.
    /// </summary>
    private void OpenSelectionEditor()
    {
        if (PlanToolArmed()) return;
        if (_selectedOpeningId is Guid openingId && _session.Document.Openings.Any(item => item.Id == openingId))
        {
            var views = new OpeningFamilyViewsWindow(_session, openingId) { Owner = this };
            if (views.ShowDialog() == true)
            {
                StatusText.Text = GetText("Status.OpeningTypeEdited");
                RefreshAll();
            }
            return;
        }
        if (_selectedWallId is Guid wallId && _session.Document.Walls.FirstOrDefault(item => item.Id == wallId) is { } wall)
        {
            var editor = new WallTypeEditorWindow(_session, wall.WallTypeId) { Owner = this };
            if (editor.ShowDialog() == true)
            {
                StatusText.Text = GetText("Status.WallTypeUpdated");
                RefreshAll();
            }
            return;
        }
        TryEditSelectedElementType();
    }

    /// <summary>Whether a drawing, placing or picking tool is waiting for a click.</summary>
    private bool PlanToolArmed() =>
        _placingOpening is not null || _movingElement || _drawingRoom || _drawingColumn || _drawingWall ||
        _awaitingSimilarWallPick || _awaitingGroupPick || _groupToPlace is not null || _pickingWorkPlane ||
        _pickWallAction is not null || _structuralTool is not null || _slabSketch is not null || _placingTopoPoints ||
        _electricalTool is not null || _wiring;

    private ElementPicker Picker => _picker ??= new ElementPicker(_session.Document);

    /// <summary>The element that currently owns selection, whichever view selected it.</summary>
    private (PickedKind Kind, Guid Id)? CurrentSelection()
    {
        if (_selectedOpeningId is Guid opening) return (PickedKind.Opening, opening);
        if (_selectedWallId is Guid wall) return (PickedKind.Wall, wall);
        if (_selectedFloorId is Guid floor) return (PickedKind.Floor, floor);
        if (_selectedColumnId is Guid column) return (PickedKind.Column, column);
        if (_selectedStructuralId is Guid structural) return (PickedKind.Structural, structural);
        return null;
    }

    /// <summary>
    /// A left click in the 3D view or the render preview. With a tool armed it works like a click in the plan, at the model
    /// point under the cursor; otherwise it selects the element there.
    /// </summary>
    private void HandleViewClick(Point position, FrameworkElement surface)
    {
        var ray = _navigation.PixelRay((float)position.X, (float)position.Y, (float)surface.ActualWidth, (float)surface.ActualHeight);
        var hit = Picker.Pick(_navigation.Position, ray);
        if (PlanToolArmed())
        {
            PlaceFromView(ray, hit);
            return;
        }
        // Linked models are not editable, and a click in the sky clears the selection.
        if (hit is not { } picked)
        {
            SelectExtendedElement();
            UpdateModelSelection(true);
            return;
        }
        switch (picked.Kind)
        {
            case PickedKind.Opening when _session.Document.Openings.FirstOrDefault(item => item.Id == picked.Id) is { } opening:
                SelectExtendedElement();
                _selectedOpeningId = picked.Id;
                _selectedWallId = opening.WallId;
                _selectedWallOffset = opening.Offset;
                break;
            case PickedKind.Wall:
                SelectExtendedElement();
                _selectedWallId = picked.Id;
                _selectedWallOffset = null;
                break;
            case PickedKind.Floor:
                SelectExtendedElement(floorId: picked.Id);
                break;
            case PickedKind.Column:
                SelectExtendedElement(columnId: picked.Id);
                break;
            case PickedKind.Structural:
                SelectExtendedElement(structuralId: picked.Id);
                break;
        }
        RefreshProperties();
        RenderPlan();
        UpdateModelSelection(true);
    }

    /// <summary>
    /// Replays a click into the plan's handlers at the model point under the cursor: on the surface that was hit, or else on
    /// the active level's floor plane. The element under the cursor travels as the click's source, so tools that pick an
    /// existing wall or room work too. Everything the plan can create can therefore be created from the 3D view.
    /// </summary>
    private void PlaceFromView(Vector3 ray, PickedElement? hit)
    {
        Vector3? point = null;
        if (hit is { } picked) point = _navigation.Position + ray * picked.Distance;
        else if (Math.Abs(ray.Z) > 1e-4f)
        {
            var level = _session.Document.Levels.FirstOrDefault(item => item.Id == _activeLevelId);
            var t = ((float)(level?.Elevation ?? 0) - _navigation.Position.Z) / ray.Z;
            if (t > 0 && t < 1000) point = _navigation.Position + ray * t;
        }
        if (point is not { } where)
        {
            StatusText.Text = GetText("Status.View3DNoPoint");
            return;
        }
        var target = hit is { } element ? (element.Kind == PickedKind.Opening ? element.HostId : element.Id) : null;
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = new FrameworkElement { Tag = target }
        };
        _planPointerOverride = WorldToCanvas(new Point2(where.X, where.Y));
        try
        {
            PlanCanvas_MouseLeftButtonDown(PlanCanvas, args);
        }
        finally
        {
            _planPointerOverride = null;
        }
        UpdateModelSelection(true);
    }

    /// <summary>
    /// Redraws the outline and temporary dimensions of the selected element. With <paramref name="rebuild"/> false only the
    /// dimension labels are moved, which is all a camera movement needs.
    /// </summary>
    private void UpdateModelSelection(bool rebuild)
    {
        if (ModelOverlay is null || _session is null) return;
        if (ViewTabs.SelectedIndex != Model3DTabIndex)
        {
            ClearModelSelectionVisuals();
            return;
        }
        if (rebuild || _shownSelection != CurrentSelection()) RebuildModelSelection();
        PositionDimensionChips();
    }

    private void ClearModelSelectionVisuals()
    {
        foreach (var visual in _selectionVisuals) ModelViewport.Children.Remove(visual);
        _selectionVisuals.Clear();
        ModelOverlay.Children.Clear();
        _dimensionChips.Clear();
        _shownSelection = null;
    }

    private void RebuildModelSelection()
    {
        ClearModelSelectionVisuals();
        if (CurrentSelection() is not { } selection) return;
        _shownSelection = selection;
        var outline = Picker.Outline(selection.Kind, selection.Id);
        var dimensions = Picker.Dimensions(selection.Kind, selection.Id);
        if (outline.Count == 0) return;

        var thickness = EdgeThickness();
        AddSelectionVisual(ScenePresentation.ToWpfEdges(outline, Matrix4x4.Identity, thickness * 2.2f, Color.FromRgb(240, 130, 20)));
        AddSelectionVisual(ScenePresentation.ToWpfEdges(dimensions.Select(line => new EdgeSegment(line.A, line.B)),
            Matrix4x4.Identity, thickness * 0.9f, Color.FromRgb(30, 110, 200)));

        foreach (var line in dimensions)
        {
            var text = new TextBlock
            {
                Text = line.Meters.ToString("0.00", CultureInfo.CurrentCulture) + " m", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(20, 70, 140))
            };
            var chip = new Border
            {
                Child = text, Padding = new Thickness(5, 1, 5, 1), CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 110, 200)), BorderThickness = new Thickness(1)
            };
            ModelOverlay.Children.Add(chip);
            _dimensionChips.Add((chip, (line.A + line.B) / 2));
        }
    }

    private void AddSelectionVisual(GeometryModel3D? model)
    {
        if (model is null) return;
        var visual = new ModelVisual3D { Content = model };
        _selectionVisuals.Add(visual);
        ModelViewport.Children.Add(visual);
    }

    /// <summary>Places each dimension label on the screen position of its measure; labels behind the camera are hidden.</summary>
    private void PositionDimensionChips()
    {
        var width = (float)ModelSurface.ActualWidth;
        var height = (float)ModelSurface.ActualHeight;
        foreach (var (chip, middle) in _dimensionChips)
        {
            if (_navigation.Project(middle, width, height) is not { } pixel || pixel.X < -50 || pixel.Y < -20 ||
                pixel.X > width + 50 || pixel.Y > height + 20)
            {
                chip.Visibility = Visibility.Collapsed;
                continue;
            }
            chip.Visibility = Visibility.Visible;
            chip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(chip, pixel.X - chip.DesiredSize.Width / 2);
            Canvas.SetTop(chip, pixel.Y - chip.DesiredSize.Height / 2);
        }
    }
}
