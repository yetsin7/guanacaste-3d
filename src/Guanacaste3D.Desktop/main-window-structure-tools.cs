using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>How a structural tool collects its geometry from the plan canvas.</summary>
internal enum StructuralPlacementMode
{
    /// <summary>One click places the element at a point.</summary>
    Point,

    /// <summary>Two clicks define the start and end of a line element.</summary>
    Line,

    /// <summary>Two opposite corners define a rectangle that is filled with beams, openings, or shafts.</summary>
    Rectangle,

    /// <summary>Two opposite corners define a rectangle whose elements become one model group.</summary>
    Group
}

/// <summary>Places structural, foundation, connection, and drafting elements from the Estructura ribbon.</summary>
public partial class MainWindow
{
    /// <summary>Center-to-center spacing of the beams created by the beam-system tool, in meters.</summary>
    private const double StructuralBeamSpacing = 1.20;

    /// <summary>Radius in meters within which a connection snaps to an existing member end or column.</summary>
    private const double StructuralJointSnapRadius = 0.60;

    private (StructuralElementKind Kind, StructuralPlacementMode Mode)? _structuralTool;
    private Point2? _pendingStructuralStart;
    private Point2? _structuralPreviewPoint;
    private Guid? _selectedStructuralId;

    /// <summary>
    /// Maps a Structure ribbon command to its tool. Commands that reuse an existing workflow are delegated
    /// to it; commands whose BIM operation is not modeled yet return false and keep the planned message.
    /// </summary>
    private bool TryExecuteStructuralCommand(string commandId)
    {
        var eventArguments = new RoutedEventArgs();
        // The catalog sends its own ids ("Beam"); the table below is keyed by label key ("RibbonCommand.Beam").
        commandId = commandId switch
        {
            "StructColumn" => "RibbonCommand.Column",
            "WallByFace" => "RibbonCommand.WallFoundation",
            "ConnectionAuto" => "RibbonCommand.ConnectionAutomation",
            _ when !commandId.StartsWith("RibbonCommand.", StringComparison.Ordinal) => "RibbonCommand." + commandId,
            _ => commandId
        };
        switch (commandId)
        {
            case "RibbonCommand.LoadFamily":
                ImportFamily_Click(this, eventArguments); return true;
            case "RibbonCommand.Beam":
                BeginStructuralTool(StructuralElementKind.Beam, StructuralPlacementMode.Line); return true;
            case "RibbonCommand.Truss":
                BeginStructuralTool(StructuralElementKind.Truss, StructuralPlacementMode.Line); return true;
            case "RibbonCommand.Brace":
                BeginStructuralTool(StructuralElementKind.Brace, StructuralPlacementMode.Line); return true;
            case "RibbonCommand.BeamSystem":
                BeginStructuralTool(StructuralElementKind.Beam, StructuralPlacementMode.Rectangle); return true;
            case "RibbonCommand.WallFoundation":
                BeginStructuralTool(StructuralElementKind.WallFoundation, StructuralPlacementMode.Line); return true;
            case "RibbonCommand.IsolatedFoundation":
                BeginStructuralTool(StructuralElementKind.IsolatedFoundation, StructuralPlacementMode.Point); return true;
            case "RibbonCommand.Component":
                BeginStructuralTool(StructuralElementKind.Component, StructuralPlacementMode.Point); return true;
            case "RibbonCommand.Connection":
                BeginStructuralTool(StructuralElementKind.Connection, StructuralPlacementMode.Point); return true;
            case "RibbonCommand.ConnectionAutomation":
                CreateConnectionsAtJoints(); return true;
            case "RibbonCommand.ModelLine":
                BeginStructuralTool(StructuralElementKind.ModelLine, StructuralPlacementMode.Line); return true;
            case "RibbonCommand.OpeningByFace":
                BeginStructuralTool(StructuralElementKind.Opening, StructuralPlacementMode.Point); return true;
            case "RibbonCommand.VerticalOpening":
                BeginStructuralTool(StructuralElementKind.Opening, StructuralPlacementMode.Rectangle); return true;
            case "RibbonCommand.ShaftOpening":
                BeginStructuralTool(StructuralElementKind.Shaft, StructuralPlacementMode.Rectangle); return true;
            case "RibbonCommand.ModelText":
                BeginStructuralTool(StructuralElementKind.ModelText, StructuralPlacementMode.Point); return true;
            case "RibbonCommand.DormerOpening":
                BeginStructuralTool(StructuralElementKind.Dormer, StructuralPlacementMode.Rectangle); return true;
            case "RibbonCommand.ReferencePlane":
                BeginKindTool(StructuralElementKind.ReferencePlane); return true;
            case "RibbonCommand.ArchitecturalLibrary":
                OpenArchitecturalLibrary(); return true;
            case "RibbonCommand.ModelGroup":
                BeginStructuralTool(StructuralElementKind.ModelLine, StructuralPlacementMode.Group); return true;
            case "RibbonCommand.Column":
                Column_Click(this, eventArguments); return true;
            case "RibbonCommand.FoundationSlab":
                Floor_Click(this, eventArguments); return true;
            case "RibbonCommand.WallOpening":
                Window_Click(this, eventArguments); return true;
            case "RibbonCommand.Level":
                AddLevel_Click(this, eventArguments); return true;
            case "RibbonCommand.Grid":
            case "RibbonCommand.ShowWorkPlane":
                ToggleGrid_Click(this, eventArguments); return true;
            case "RibbonCommand.SetWorkPlane":
                Levels_Click(this, eventArguments); return true;
            case "RibbonCommand.Viewer":
                ShowModel_Click(this, eventArguments); return true;
            default:
                return false;
        }
    }

    /// <summary>Arms a placement tool; leaves any other drawing tool and restores the plan view.</summary>
    private void BeginStructuralTool(StructuralElementKind kind, StructuralPlacementMode mode)
    {
        SetWallTool(false);
        CancelExtendedDrawing();
        _selectedViewId = null;
        ViewTabs.SelectedIndex = 0;
        _structuralTool = (kind, mode);
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        StatusText.Text = mode switch
        {
            StructuralPlacementMode.Group => GetText("Status.ModelGroupFirstCorner"),
            StructuralPlacementMode.Point => FormatText("Status.StructuralPoint", GetStructuralName(kind)),
            StructuralPlacementMode.Rectangle => GetText(kind switch
            {
                StructuralElementKind.Beam => "Status.StructuralFirstCorner",
                StructuralElementKind.Dormer => "Status.DormerFirstCorner",
                StructuralElementKind.Skylight => "Status.SkylightFirstCorner",
                StructuralElementKind.Area => "Status.AreaFirstCorner",
                _ => "Status.OpeningFirstCorner"
            }),
            _ => FormatText("Status.StructuralFirstPoint", GetStructuralName(kind))
        };
        RenderPlan();
    }

    /// <summary>Disarms the placement tool and drops its provisional geometry.</summary>
    private void CancelStructuralTool()
    {
        _structuralTool = null;
        _structuralPreset = null;
        _tagTarget = null;
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
    }

    /// <summary>Tracks the pointer while a structural tool is armed so its preview follows the cursor.</summary>
    private bool TryUpdateStructuralPreview(MouseEventArgs e)
    {
        if (_structuralTool is not { } tool) return false;
        var pointer = SnapWallPoint(CanvasToWorld(e.GetPosition(PlanCanvas)));
        _structuralPreviewPoint = ResolveStructuralPoint(tool.Kind, pointer);
        RenderPlan();
        return true;
    }

    /// <summary>Places a point element at one click, or collects the two ends of a line or rectangle.</summary>
    private bool TryHandleStructuralPlacement(MouseButtonEventArgs e)
    {
        if (TryHandleTagClick(e)) return true;
        if (_structuralTool is not { } tool) return false;
        var point = ResolveStructuralPoint(tool.Kind,
            SnapWallPoint(CanvasToWorld(e.GetPosition(PlanCanvas))));
        if (tool.Mode == StructuralPlacementMode.Point)
        {
            if (tool.Kind == StructuralElementKind.ModelText) PlaceModelText(point);
            else if (tool.Kind == StructuralElementKind.Opening) CreateOpeningFromTool(tool.Kind, point, point);
            else CreateStructuralFromTool(tool.Kind, point, point);
            return true;
        }

        if (_pendingStructuralStart is not Point2 start)
        {
            _pendingStructuralStart = point;
            _structuralPreviewPoint = point;
            StatusText.Text = tool.Mode switch
            {
                StructuralPlacementMode.Group => GetText("Status.ModelGroupSecondCorner"),
                StructuralPlacementMode.Rectangle when tool.Kind == StructuralElementKind.Beam =>
                    GetText("Status.StructuralSecondCorner"),
                StructuralPlacementMode.Rectangle when tool.Kind == StructuralElementKind.Dormer =>
                    GetText("Status.DormerSecondCorner"),
                StructuralPlacementMode.Rectangle when tool.Kind == StructuralElementKind.Skylight =>
                    GetText("Status.SkylightSecondCorner"),
                StructuralPlacementMode.Rectangle when tool.Kind == StructuralElementKind.Area =>
                    GetText("Status.AreaSecondCorner"),
                StructuralPlacementMode.Rectangle => GetText("Status.OpeningSecondCorner"),
                _ => FormatText("Status.StructuralSecondPoint", GetStructuralName(tool.Kind))
            };
            RenderPlan();
            return true;
        }

        if (tool.Mode == StructuralPlacementMode.Group)
        {
            _pendingStructuralStart = null;
            _structuralPreviewPoint = null;
            var members = _session.AddStructuralGroup(_activeLevelId, start, point);
            StatusText.Text = members == 0
                ? GetText("Status.ModelGroupNeedsTwo")
                : FormatText("Status.ModelGroupCreated", members);
            RefreshAll();
            return true;
        }

        if (tool.Kind == StructuralElementKind.Beam)
        {
            var count = _session.AddBeamSystem(_activeLevelId, GetStructuralName(tool.Kind),
                start, point, StructuralBeamSpacing);
            StatusText.Text = FormatText("Status.BeamSystemCreated", count);
        }
        else if (tool.Kind is StructuralElementKind.Opening or StructuralElementKind.Shaft)
        {
            CreateOpeningFromTool(tool.Kind, start, point);
            return true;
        }
        else if (tool.Kind == StructuralElementKind.Dormer)
        {
            CreateDormerFromTool(start, point);
            return true;
        }
        else if (tool.Kind == StructuralElementKind.Skylight)
        {
            CreateSkylightFromTool(start, point);
            return true;
        }
        else if (tool.Kind == StructuralElementKind.Area)
        {
            CreateAreaFromTool(start, point);
            return true;
        }
        else
        {
            CreateStructuralFromTool(tool.Kind, start, point);
            return true;
        }
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        RefreshAll();
        return true;
    }

    /// <summary>
    /// Adds an opening or shaft through the session, which rejects rectangles that do not fit inside a floor.
    /// The pending corner is cleared either way so the user can try again from the next click.
    /// </summary>
    private void CreateOpeningFromTool(StructuralElementKind kind, Point2 start, Point2 end)
    {
        var id = _session.AddOpening(_activeLevelId, kind, GetStructuralName(kind), start, end);
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        if (id is not Guid created)
        {
            StatusText.Text = GetText(kind == StructuralElementKind.Shaft ? "Status.ShaftNoFloor" : "Status.OpeningNoFloor");
            RefreshAll();
            return;
        }
        _selectedStructuralId = created;
        StatusText.Text = FormatText("Status.StructuralCreated", GetStructuralName(kind));
        RefreshAll();
    }

    /// <summary>
    /// Adds a dormer on the roof under its rectangle. The session rejects rectangles that are not fully
    /// inside a roof of this level, and the pending corner is cleared either way.
    /// </summary>
    private void CreateDormerFromTool(Point2 start, Point2 end)
    {
        var id = _session.AddDormer(_activeLevelId, GetStructuralName(StructuralElementKind.Dormer), start, end);
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        if (id is not Guid created)
        {
            StatusText.Text = GetText("Status.DormerNoRoof");
            RefreshAll();
            return;
        }
        _selectedStructuralId = created;
        StatusText.Text = FormatText("Status.StructuralCreated", GetStructuralName(StructuralElementKind.Dormer));
        RefreshAll();
    }

    /// <summary>Asks for the annotation text and places it at the clicked position; cancelling places nothing.</summary>
    private void PlaceModelText(Point2 position)
    {
        var text = TextPrompt.Ask(this, GetText("Prompt.ModelText.Title"), GetText("Prompt.ModelText.Label"),
            GetText("Prompt.Accept"), GetText("Prompt.Cancel"));
        if (text is null)
        {
            StatusText.Text = GetText("Status.ModelTextCancelled");
            return;
        }
        _selectedStructuralId = _session.AddModelText(_activeLevelId, position, text);
        StatusText.Text = FormatText("Status.StructuralCreated", text);
        RefreshAll();
    }

    /// <summary>Adds one element from the active tool, selects it, and keeps the tool armed for the next one.</summary>
    private void CreateStructuralFromTool(StructuralElementKind kind, Point2 start, Point2 end)
    {
        try
        {
            _selectedStructuralId = _structuralPreset is { } preset && preset.Kind == kind
                ? _session.AddElementFromPreset(_activeLevelId, preset, start, end)
                : _session.AddStructuralElement(_activeLevelId, kind, GetStructuralName(kind), start, end);
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
            return;
        }
        _pendingStructuralStart = null;
        _structuralPreviewPoint = null;
        StatusText.Text = FormatText("Status.StructuralCreated", GetStructuralName(kind));
        RefreshAll();
    }

    /// <summary>Creates connections at every unconnected joint on the active level.</summary>
    private void CreateConnectionsAtJoints()
    {
        CancelStructuralTool();
        var count = _session.AddConnectionsAtJoints(_activeLevelId, GetStructuralName(StructuralElementKind.Connection));
        StatusText.Text = count == 0
            ? GetText("Status.ConnectionsNone")
            : FormatText("Status.ConnectionsCreated", count);
        RefreshAll();
    }

    /// <summary>Snaps a connection to the nearest member end or column center, otherwise keeps the point.</summary>
    private Point2 ResolveStructuralPoint(StructuralElementKind kind, Point2 point)
    {
        if (kind != StructuralElementKind.Connection) return point;
        var candidates = _session.Document.StructuralElements
            .Where(element => element.LevelId == _activeLevelId && element.Length > 0)
            .SelectMany(element => new[] { element.Start, element.End })
            .Concat(_session.Document.Columns
                .Where(column => column.LevelId == _activeLevelId)
                .Select(column => column.Position))
            .Where(candidate => candidate.DistanceTo(point) <= StructuralJointSnapRadius)
            .ToArray();
        return candidates.Length == 0 ? point : candidates.MinBy(candidate => candidate.DistanceTo(point));
    }

    /// <summary>Resolves the localized display name used for a structural element kind.</summary>
    private string GetStructuralName(StructuralElementKind kind) => GetText(ArchitecturalNameKey(kind) ?? kind switch
    {
        StructuralElementKind.Stair => "Tool.Stair",
        StructuralElementKind.Railing => "Tool.Railing",
        StructuralElementKind.Beam => "Entity.Beam",
        StructuralElementKind.Brace => "Entity.Brace",
        StructuralElementKind.Truss => "Entity.Truss",
        StructuralElementKind.WallFoundation => "Entity.WallFoundation",
        StructuralElementKind.IsolatedFoundation => "Entity.IsolatedFoundation",
        StructuralElementKind.Component => "Entity.Component",
        StructuralElementKind.Connection => "Entity.Connection",
        StructuralElementKind.Opening => "Entity.Opening",
        StructuralElementKind.Shaft => "Entity.Shaft",
        StructuralElementKind.ModelText => "Entity.ModelText",
        StructuralElementKind.Dormer => "Entity.Dormer",
        _ => "Entity.ModelLine"
    });

}
