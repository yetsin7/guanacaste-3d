using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>Placement of catalog types and the Architecture tab tools that sit on top of the structural tool engine.</summary>
public partial class MainWindow
{
    /// <summary>What a tag click renames.</summary>
    private enum TagTarget { Room, Area }

    private ElementPreset? _structuralPreset;
    private ColumnPreset? _columnPreset;
    private Guid? _placingOpeningTypeId;
    private TagTarget? _tagTarget;

    /// <summary>The way each element kind collects its geometry from the plan.</summary>
    private static StructuralPlacementMode PlacementModeFor(StructuralElementKind kind) => kind switch
    {
        StructuralElementKind.Downspout or StructuralElementKind.IsolatedFoundation or
            StructuralElementKind.Component or StructuralElementKind.Connection or
            StructuralElementKind.ModelText => StructuralPlacementMode.Point,
        StructuralElementKind.Skylight or StructuralElementKind.Area or StructuralElementKind.Dormer or
            StructuralElementKind.Opening or StructuralElementKind.Shaft => StructuralPlacementMode.Rectangle,
        _ => StructuralPlacementMode.Line
    };

    /// <summary>Arms the placement tool for a catalog section, so every element placed takes its size and style.</summary>
    internal void BeginPresetTool(ElementPreset preset)
    {
        BeginStructuralTool(preset.Kind, PlacementModeFor(preset.Kind));
        _structuralPreset = preset;
        StatusText.Text = FormatText("Status.PresetArmed", preset.Name);
    }

    /// <summary>Arms a tool by element kind with the kind's default section.</summary>
    private void BeginKindTool(StructuralElementKind kind) => BeginStructuralTool(kind, PlacementModeFor(kind));

    /// <summary>Lets the library window arm a tool by kind.</summary>
    internal void BeginKindToolFromLibrary(StructuralElementKind kind) => BeginKindTool(kind);

    /// <summary>Opens the architectural library, the type browser for every element the Architecture tab places.</summary>
    private void OpenArchitecturalLibrary()
    {
        var window = new ArchitecturalLibraryWindow(this, _session) { Owner = this };
        window.Show();
    }

    /// <summary>Starts wall drawing with a library wall assembly selected in the type picker.</summary>
    internal void PlaceLibraryWall(WallType wallType)
    {
        var item = WallTypePicker.Items.OfType<ComboBoxItem>().FirstOrDefault(candidate => candidate.Tag is Guid id && id == wallType.Id);
        if (item is not null) WallTypePicker.SelectedItem = item;
        Wall_Click(this, new RoutedEventArgs());
    }

    /// <summary>Starts door or window placement with a specific type.</summary>
    internal void PlaceLibraryOpening(OpeningType type)
    {
        if (type.Kind == OpeningKind.Door) Door_Click(this, new RoutedEventArgs());
        else Window_Click(this, new RoutedEventArgs());
        _placingOpeningTypeId = type.Id;
        StatusText.Text = FormatText("Status.PresetArmed", type.DisplayName);
    }

    /// <summary>Starts column placement with a catalog section.</summary>
    internal void PlaceLibraryColumn(ColumnPreset preset)
    {
        Column_Click(this, new RoutedEventArgs());
        _columnPreset = preset;
        StatusText.Text = FormatText("Status.PresetArmed", preset.Name);
    }

    /// <summary>Creates a floor, ceiling or roof from the walls of the active level with a catalog recipe.</summary>
    internal void PlaceLibrarySlab(SlabPreset preset) => CreateEnclosureSlab(preset.Kind, preset);

    /// <summary>Shows the catalog recipes for a slab role at the pointer, so the user picks a type before it is created.</summary>
    private void ShowSlabPresetMenu(FloorKind kind, Action<SlabPreset>? onPick = null)
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        foreach (var preset in ArchitecturalLibrary.SlabPresets.Where(candidate => candidate.Kind == kind))
        {
            var recipe = preset;
            var item = new MenuItem { Header = preset.Name };
            item.Click += (_, _) => { if (onPick is null) CreateEnclosureSlab(kind, recipe); else onPick(recipe); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>Starts a tag tool: each click on a room or area asks for a new name.</summary>
    private void BeginTagTool(TagTarget target)
    {
        BeginStructuralTool(StructuralElementKind.ModelLine, StructuralPlacementMode.Line);
        CancelStructuralTool();
        _tagTarget = target;
        StatusText.Text = GetText(target == TagTarget.Room ? "Status.TagRoomClick" : "Status.TagAreaClick");
        RenderPlan();
    }

    /// <summary>Renames the room or area under the click and keeps the tag tool armed.</summary>
    private bool TryHandleTagClick(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_tagTarget is not { } target) return false;
        var point = CanvasToWorld(e.GetPosition(PlanCanvas));
        Guid? id;
        string current;
        if (target == TagTarget.Room)
        {
            var room = _session.Document.Rooms.LastOrDefault(candidate => candidate.LevelId == _activeLevelId &&
                Math.Min(candidate.FirstCorner.X, candidate.SecondCorner.X) <= point.X &&
                point.X <= Math.Max(candidate.FirstCorner.X, candidate.SecondCorner.X) &&
                Math.Min(candidate.FirstCorner.Y, candidate.SecondCorner.Y) <= point.Y &&
                point.Y <= Math.Max(candidate.FirstCorner.Y, candidate.SecondCorner.Y));
            id = room?.Id;
            current = room?.Name ?? string.Empty;
        }
        else
        {
            var area = _session.Document.StructuralElements.LastOrDefault(candidate =>
                candidate.LevelId == _activeLevelId && candidate.Kind == StructuralElementKind.Area &&
                candidate.PlanExtent.Contains(point));
            id = area?.Id;
            current = area?.Name ?? string.Empty;
        }
        if (id is not Guid target_id)
        {
            StatusText.Text = GetText("Status.TagNothingFound");
            return true;
        }
        var name = TextPrompt.Ask(this, GetText(target == TagTarget.Room ? "Prompt.TagRoom.Title" : "Prompt.TagArea.Title"),
            GetText("Prompt.Tag.Label"), GetText("Prompt.Accept"), GetText("Prompt.Cancel"), current);
        if (name is null)
        {
            StatusText.Text = GetText("Status.TagCancelled");
            return true;
        }
        try
        {
            if (target == TagTarget.Room) _session.RenameRoom(target_id, name);
            else _session.RenameStructuralElement(target_id, name);
            StatusText.Text = FormatText("Status.TagRenamed", name);
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
        }
        RefreshAll();
        return true;
    }

    /// <summary>Adds a skylight on the roof under its rectangle; the session rejects rectangles not fully on a roof.</summary>
    private void CreateSkylightFromTool(Point2 start, Point2 end)
    {
        var id = _session.AddSkylight(_activeLevelId, GetStructuralName(StructuralElementKind.Skylight), start, end);
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        if (id is not Guid created)
        {
            StatusText.Text = GetText("Status.SkylightNoRoof");
            RefreshAll();
            return;
        }
        _selectedStructuralId = created;
        StatusText.Text = FormatText("Status.StructuralCreated", GetStructuralName(StructuralElementKind.Skylight));
        RefreshAll();
    }

    /// <summary>Adds an area rectangle of the area scheme.</summary>
    private void CreateAreaFromTool(Point2 start, Point2 end)
    {
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        try
        {
            _selectedStructuralId = _session.AddArea(_activeLevelId, GetStructuralName(StructuralElementKind.Area), start, end);
            StatusText.Text = FormatText("Status.StructuralCreated", GetStructuralName(StructuralElementKind.Area));
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
        }
        RefreshAll();
    }

    /// <summary>
    /// Opens the property editor for the current selection: slabs and roofs, columns, and structural or
    /// architectural elements. Returns false when the selection has no such properties.
    /// </summary>
    private bool TryEditSelectedElementType(bool gridFocus = false)
    {
        ElementEditorWindow? editor = null;
        if (_selectedStructuralId is Guid structuralId &&
            _session.Document.StructuralElements.Any(element => element.Id == structuralId))
            editor = ElementEditorWindow.ForStructural(_session, structuralId, gridFocus);
        else if (_selectedFloorId is Guid floorId && _session.Document.Floors.Any(floor => floor.Id == floorId))
            editor = ElementEditorWindow.ForFloor(_session, floorId);
        else if (_selectedColumnId is Guid columnId && _session.Document.Columns.Any(column => column.Id == columnId))
            editor = ElementEditorWindow.ForColumn(_session, columnId);
        if (editor is null) return false;
        editor.Owner = this;
        if (editor.ShowDialog() == true) StatusText.Text = GetText("Status.ElementUpdated");
        RefreshAll();
        return true;
    }

    /// <summary>Curtain grid and mullion buttons edit the selected curtain wall, or explain what to select.</summary>
    private void EditCurtainWall(bool gridFocus)
    {
        var selected = _session.Document.StructuralElements
            .FirstOrDefault(element => element.Id == _selectedStructuralId);
        if (selected?.Kind != StructuralElementKind.CurtainWall)
        {
            StatusText.Text = GetText("Status.SelectCurtainWall");
            return;
        }
        TryEditSelectedElementType(gridFocus);
    }

    /// <summary>Resolves the localized label of a structural kind for the new architectural elements.</summary>
    private static string? ArchitecturalNameKey(StructuralElementKind kind) => kind switch
    {
        StructuralElementKind.Ramp => "Tool.Ramp",
        StructuralElementKind.Gutter => "Entity.Gutter",
        StructuralElementKind.Downspout => "Entity.Downspout",
        StructuralElementKind.CurtainWall => "Entity.CurtainWall",
        StructuralElementKind.Skylight => "Entity.Skylight",
        StructuralElementKind.ReferencePlane => "Entity.ReferencePlane",
        StructuralElementKind.RoomSeparator => "Entity.RoomSeparator",
        StructuralElementKind.AreaBoundary => "Entity.AreaBoundary",
        StructuralElementKind.Area => "Entity.Area",
        StructuralElementKind.WallSweep => "Entity.WallSweep",
        StructuralElementKind.WallReveal => "Entity.WallReveal",
        StructuralElementKind.Soffit => "Entity.Soffit",
        StructuralElementKind.Fascia => "Entity.Fascia",
        StructuralElementKind.SlabEdge => "Entity.SlabEdge",
        _ => null
    };
}
