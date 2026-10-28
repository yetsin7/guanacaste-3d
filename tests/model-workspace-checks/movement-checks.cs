using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Navigation;

internal static partial class WorkspaceChecks
{
    /// <summary>Checks forward-ray intersections, singular views, and floating-point failure containment.</summary>
    private static void ProjectionChecks()
    {
        var plane = new ModelDragPlane(Vector3.Zero, Vector3.UnitY);
        Check(plane.Intersect(new(0, -10, 2), Vector3.UnitY) is Vector3 point && Vector3.Distance(point, new(0, 0, 2)) < 1e-5,
            "Wall-plane picking preserves world height");
        Check(plane.Intersect(new(0, -10, 2), Vector3.UnitX) is null &&
              plane.Intersect(new(0, -10, 2), -Vector3.UnitY) is null &&
              plane.Intersect(new(float.NaN), Vector3.UnitY) is null,
            "Parallel, backward, and nonfinite edit rays are rejected");
    }

    /// <summary>Exercises direct window drags, base/destination clicks, invalid placements, Escape, and one-step history.</summary>
    private static void OpeningMovementChecks()
    {
        Guid wallId = default, windowId = default, doorId = default;
        var session = UseDocument((document, level) =>
        {
            wallId = document.AddWall(level, new(-6, 0), new(6, 0), height: 4);
            windowId = document.AddOpening(wallId, OpeningKind.Window, 3, sill: 1, height: 1);
            doorId = document.AddOpening(wallId, OpeningKind.Door, 8);
        });
        var original = session.Document.Openings.Single(opening => opening.Id == windowId);
        var startWorld = new Vector3(-3, -0.1f, 1.5f);
        var start = Aim(startWorld);
        Select(start);
        Check(Field<Guid?>("_selectedOpeningId") == windowId, "Ray picking selects the window through its visible face");
        var navigator = Field<object>("_modelNavigator");
        var beginDrag = (Func<Point, bool>)navigator.GetType().GetProperty("BeginElementDrag")!.GetValue(navigator)!;
        Check(beginDrag(start), "Dragging a selected window is claimed by the editing tool");
        var target = Pixel(startWorld + new Vector3(1, 0, 0.4f));
        Invoke("UpdateModelMove", target);
        Check(Field<bool>("_modelMoveValid") && session.Document.Openings.Single(item => item.Id == windowId) == original,
            "The moving window previews along its wall and in height without mutating the document");
        Check(Field<System.Collections.IList>("_modelMoveVisuals").Count > 0, "A three-dimensional ghost is visible during movement");
        SaveBitmap("window-moving.png");
        var history = session.UndoNames.Count;
        Invoke("CompleteModelMove", target);
        var moved = session.Document.Openings.Single(opening => opening.Id == windowId);
        Check(Math.Abs(moved.Offset - 4) < 1e-4 && Math.Abs(moved.Sill - 1.4) < 1e-4 && session.UndoNames.Count == history + 1,
            "Release commits horizontal offset and sill together as one transaction");
        session.Undo();
        Check(session.Document.Openings.Single(item => item.Id == windowId) == original, "One undo restores both window coordinates");
        session.Redo();
        Check(session.Document.Openings.Single(item => item.Id == windowId) == moved, "Redo restores the coordinated move");
        Invoke("RefreshAll");
        WaitFor(() => !Field<bool>("_modelDirty"));

        var currentWorld = new Vector3(-2, -0.1f, 1.9f);
        start = Aim(currentWorld);
        Select(start);
        Invoke("BeginMoveTool");
        Select(start);
        target = Pixel(currentWorld + new Vector3(4, 0, 0));
        Invoke("UpdateModelMove", target);
        history = session.UndoNames.Count;
        Check(!Field<bool>("_modelMoveValid"), "Overlapping a neighboring door produces a blocked preview");
        Select(target);
        Check(session.Document.Openings.Single(item => item.Id == windowId) == moved && session.UndoNames.Count == history,
            "An invalid destination changes neither the model nor undo history");

        Invoke("BeginMoveTool");
        Select(start);
        target = Pixel(currentWorld + new Vector3(0, 0, 4));
        Invoke("UpdateModelMove", target);
        Check(!Field<bool>("_modelMoveValid"), "A window above its host wall is blocked");
        Invoke("CancelMoveTool");
        Check(Field<System.Collections.IList>("_modelMoveVisuals").Count == 0 && session.UndoNames.Count == history,
            "Cancel removes the ghost and preserves document history");

        var doorWorld = new Vector3(2, -0.1f, 1);
        start = Aim(doorWorld);
        Select(start);
        Invoke("BeginMoveTool");
        Select(start);
        Select(Pixel(doorWorld + new Vector3(1, 0, 0.7f)));
        var door = session.Document.Openings.Single(item => item.Id == doorId);
        Check(Math.Abs(door.Offset - 9) < 1e-4 && door.Sill == 0, "Two-click 3D moves retain the door's floor constraint");

        var count = session.UndoNames.Count;
        try { session.MoveOpening(windowId, wallId, 4, -1); }
        catch (InvalidDataException) { }
        Check(session.UndoNames.Count == count && session.Document.Openings.Single(item => item.Id == windowId) == moved,
            "Core validation independently rejects negative sill edits atomically");
        var path = Path.Combine(Evidence, "moved-elements.g3");
        ProjectFile.Save(path, session.Document);
        var reopened = ProjectFile.Load(path).Openings.Single(item => item.Id == windowId);
        Check(reopened == moved, "Moved offset and sill persist through native save and reopen");

        var targetWall = session.AddWall(session.Document.Levels[0].Id, new(2, -4), new(8, -4), height: 4);
        Invoke("RefreshAll");
        WaitFor(() => !Field<bool>("_modelDirty"));
        Aim(new(0, -1, 1.5f), new(0, -16, 8));
        start = Pixel(currentWorld);
        Select(start);
        Invoke("BeginMoveTool");
        Select(start);
        target = Pixel(new(4, -4.1f, currentWorld.Z));
        Invoke("UpdateModelMove", target);
        Check(Field<bool>("_modelMoveValid") && Field<WallOpening>("_modelOpeningCandidate").WallId == targetWall,
            "A 3D destination on another visible wall previews re-hosting without moving the source window");
        Select(target);
        Check(session.Document.Openings.Single(item => item.Id == windowId).WallId == targetWall,
            "The 3D move can re-host a window onto another wall of the same level");
        session.Undo();
        Check(session.Document.Openings.Single(item => item.Id == windowId) == moved,
            "One undo restores the original host, offset, and sill after re-hosting");
    }

    /// <summary>Verifies columns and walls use the existing planar transactions from a 3D pointer gesture.</summary>
    private static void GeneralMovementChecks()
    {
        Guid columnId = default, wallId = default;
        var session = UseDocument((document, level) =>
        {
            columnId = document.AddColumn(level, new(0, 0));
            wallId = document.AddWall(level, new(-4, 4), new(4, 4));
        });
        var startWorld = new Vector3(0, -0.2f, 1);
        var start = Aim(startWorld);
        Select(start);
        Check(Field<Guid?>("_selectedColumnId") == columnId, "The 3D selection identifies a column");
        Invoke("BeginMoveTool");
        Select(start);
        Select(Pixel(startWorld + new Vector3(1, 1, 0)));
        var column = session.Document.Columns.Single(item => item.Id == columnId);
        Check(column.Position.DistanceTo(new(1, 1)) < 1e-4, "A column moves in 3D using the same planar model edit as plan");
        session.Undo();
        Check(session.Document.Columns.Single(item => item.Id == columnId).Position.DistanceTo(new(0, 0)) < 1e-4,
            "Column movement is undoable");
        Invoke("RefreshAll");
        WaitFor(() => !Field<bool>("_modelDirty"));
        startWorld = new Vector3(2, 3.9f, 1);
        start = Aim(startWorld, new(2, -4, 5));
        Select(start);
        Check(Field<Guid?>("_selectedWallId") == wallId, "The 3D ray identifies the intended wall");
        Invoke("BeginMoveTool");
        Select(start);
        Invoke("UpdateModelMove", Pixel(startWorld + new Vector3(1, -1, 0)));
        var previous = session.UndoNames.Count;
        _window.GetType().GetMethod("OnPreviewKeyDown", PrivateInstance)!.Invoke(_window,
            [new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(_window)!, 0, System.Windows.Input.Key.Escape)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent }]);
        Check(!Field<bool>("_movingElement") && session.UndoNames.Count == previous, "Escape cancels a 3D edit through the real keyboard handler");
        Invoke("BeginMoveTool");
        Select(start);
        Select(Pixel(startWorld + new Vector3(1, -1, 0)));
        var wall = session.Document.Walls.Single(item => item.Id == wallId);
        Check(wall.Start.DistanceTo(new(-3, 3)) < 1e-4 && wall.End.DistanceTo(new(5, 3)) < 1e-4,
            "The two-click 3D wall move preserves the wall section and translates both endpoints");
    }

    /// <summary>Checks that invisible slab cuts remain pickable, movable, hosted, and undoable.</summary>
    private static void SlabVoidChecks()
    {
        Guid openingId = default;
        var session = UseDocument((document, level) =>
        {
            document.AddFloor(level, [new(-5, -5), new(5, -5), new(5, 5), new(-5, 5)]);
            openingId = document.AddOpening(level, StructuralElementKind.Opening, "Floor void", new(-1, -1), new(1, 1))!.Value;
        });
        var startWorld = new Vector3(0, 0, 0);
        var start = Aim(startWorld, new(0, -4, 8));
        Select(start);
        Check(Field<Guid?>("_selectedStructuralId") == openingId, "A slab cavity is selectable even though it has no rendered solid");
        Invoke("BeginMoveTool");
        Select(start);
        Select(Pixel(startWorld + new Vector3(2, 0, 0)));
        var moved = session.Document.StructuralElements.Single(item => item.Id == openingId);
        Check(moved.Start.DistanceTo(new(1, -1)) < 1e-4, "The selected hole moves and updates the shared slab-cut definition");
        var scene = new Guanacaste3D.Core.Rendering.RenderSceneBuilder()
            .AddBuilding(Guanacaste3D.Core.Geometry.BuildingMesher.Build(session.Document)).Build();
        var query = SceneQuery.Build(scene);
        Check(query.Pick(new(0, 0, 2), -Vector3.UnitZ) is not null && query.Pick(new(2, 0, 2), -Vector3.UnitZ) is null,
            "Regenerated 3D geometry closes the old cavity and cuts the new destination");
        var count = session.UndoNames.Count;
        try { session.MoveStructuralElement(openingId, 20, 0); }
        catch (InvalidDataException) { }
        Check(session.UndoNames.Count == count && session.Document.StructuralElements.Single(item => item.Id == openingId) == moved,
            "Moving a cavity outside every slab is rejected without history changes");
        session.Undo();
        Check(session.Document.StructuralElements.Single(item => item.Id == openingId).Start.DistanceTo(new(-1, -1)) < 1e-4,
            "One undo restores the original slab cut");
        var singleClick = session.AddOpening(session.Document.Levels[0].Id, StructuralElementKind.Opening,
            "Point-placed floor void", new(-3, 2), new(-3, 2))!.Value;
        var pointOpening = session.Document.StructuralElements.Single(item => item.Id == singleClick);
        Check(Guanacaste3D.Core.Geometry.SlabOpeningHosts.Resolve(session.Document, pointOpening).Count == 1,
            "A single-click opening resolves its full default footprint instead of a zero-size click rectangle");
        session.MoveStructuralElement(singleClick, 1, 0);
        Check(session.Document.StructuralElements.Single(item => item.Id == singleClick).Start.DistanceTo(new(-2, 2)) < 1e-4,
            "Point-placed slab openings remain movable with their actual default width");
    }
}
