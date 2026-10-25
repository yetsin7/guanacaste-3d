using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private OpeningKind? _placingOpening;
    private Point2? _placementPointer;

    /// <summary>Builds a transient instance and resolves its nearest host without creating history.</summary>
    private (Wall Wall, WallOpening Opening, bool Fits) OpeningPlacement(Point2 pointer)
    {
        var kind = _placingOpening!.Value;
        var type = _session.Document.OpeningTypes.FirstOrDefault(candidate => candidate.Id == BuiltInOpeningTypes.DefaultFor(kind))
            ?? _session.Document.OpeningTypes.FirstOrDefault(candidate => candidate.Kind == kind)
            ?? BuiltInOpeningTypes.CreateDefaults().Single(candidate => candidate.Id == BuiltInOpeningTypes.DefaultFor(kind));
        var walls = _session.Document.Walls.Where(wall => wall.LevelId == _activeLevelId).ToArray();
        var fallback = new Wall(Guid.Empty, _activeLevelId,
            new Point2(pointer.X - type.Width, pointer.Y), new Point2(pointer.X + type.Width, pointer.Y), 0.2, 3);
        var opening = new WallOpening(Guid.Empty, walls.FirstOrDefault()?.Id ?? Guid.Empty, kind,
            type.Width, type.Width, type.DefaultSill, type.Height) { OpeningTypeId = type.Id };
        var host = walls.Length == 0 ? null : OpeningHosting.Resolve(opening, pointer, walls,
            _session.Document.Openings, OpeningCapturePixels / _scale);
        return host is { } candidate
            ? (walls.Single(wall => wall.Id == candidate.WallId), opening with
                { WallId = candidate.WallId, Offset = candidate.Offset }, candidate.Fits)
            : (fallback, opening with { WallId = Guid.Empty }, false);
    }

    /// <summary>Commits a valid preview at the click position; an invalid click keeps the tool armed.</summary>
    private bool TryPlaceOpening(MouseButtonEventArgs e)
    {
        if (_placingOpening is null) return false;
        e.Handled = true;
        Keyboard.Focus(PlanCanvas);
        return CommitOpeningPlacement(CanvasToWorld(e.GetPosition(PlanCanvas)));
    }

    /// <summary>Commits a model-space placement shared by pointer input and deterministic interaction checks.</summary>
    private bool CommitOpeningPlacement(Point2 pointer)
    {
        _placementPointer = pointer;
        var preview = OpeningPlacement(pointer);
        if (!preview.Fits)
        {
            StatusText.Text = GetText("Status.OpeningPlacementBlocked");
            RenderPlan();
            return true;
        }
        try
        {
            _selectedOpeningId = _session.AddOpening(preview.Wall.Id, preview.Opening.Kind,
                preview.Opening.Offset, openingTypeId: _session.Document.OpeningTypes.Any(type => type.Id == preview.Opening.OpeningTypeId)
                    ? preview.Opening.OpeningTypeId : null);
            _selectedWallId = preview.Wall.Id;
            StatusText.Text = GetText("Status.OpeningPlacement");
            RefreshAll();
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
        }
        return true;
    }

    /// <summary>Draws non-interactive ghosts at the cursor, including point tools before their first click.</summary>
    private void RenderPlacementPreview()
    {
        if (_placementPointer is not Point2 pointer) return;
        var firstChild = PlanCanvas.Children.Count;
        if (_placingOpening is not null)
        {
            var preview = OpeningPlacement(pointer);
            DrawOpeningSymbol(preview.Wall, preview.Opening,
                preview.Fits ? OpeningGhostBrush : OpeningBlockedBrush, OpeningGhostFill, null);
        }
        else if (_drawingColumn)
        {
            if (SnapGridCheck.IsChecked == true)
                pointer = new Point2(Math.Round(pointer.X * 10, MidpointRounding.AwayFromZero) / 10,
                    Math.Round(pointer.Y * 10, MidpointRounding.AwayFromZero) / 10);
            AddStructuralPreviewOutline(new[] { new Point2(pointer.X - 0.15, pointer.Y - 0.15),
                new Point2(pointer.X + 0.15, pointer.Y - 0.15), new Point2(pointer.X + 0.15, pointer.Y + 0.15),
                new Point2(pointer.X - 0.15, pointer.Y + 0.15) });
        }
        else if (_drawingRoom)
        {
            var corner = _pendingRoomStart ?? new Point2(pointer.X - 1, pointer.Y - 1);
            AddStructuralPreviewOutline(new[] { corner, new Point2(pointer.X, corner.Y), pointer,
                new Point2(corner.X, pointer.Y) });
        }
        for (var index = firstChild; index < PlanCanvas.Children.Count; index++)
        {
            PlanCanvas.Children[index].IsHitTestVisible = false;
            PlanCanvas.Children[index].Opacity = 0.65;
        }
    }
}
