using System.IO;
using System.Windows;
using Guanacaste3D.Core;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Copy, rotate, mirror and array for everything that is not a wall: columns, stairs, beams, furniture, slabs, rooms,
/// electrical devices and plumbing points. The Modify tools used to answer "select a wall first" to anything else.
/// </summary>
public partial class MainWindow
{
    private List<Guid>? _elementClipboard;

    /// <summary>The selected element that is not a wall or opening, or none.</summary>
    private Guid? SelectedPlanElementId() =>
        _selectedOpeningId is not null || _selectedWallId is not null ? null
        : _selectedColumnId ?? _selectedStructuralId ?? _selectedFloorId ?? _selectedRoomId ?? _selectedElectricalId ?? _selectedPlumbingId;

    /// <summary>The point an element turns about: its insertion point, or the middle of its outline.</summary>
    private Point2? ReferencePointOf(Guid id)
    {
        var document = _session.Document;
        if (document.Columns.FirstOrDefault(item => item.Id == id) is { } column) return column.Position;
        if (document.StructuralElements.FirstOrDefault(item => item.Id == id) is { } element)
            return new Point2((element.Start.X + element.End.X) / 2, (element.Start.Y + element.End.Y) / 2);
        if (document.Floors.FirstOrDefault(item => item.Id == id) is { } floor)
            return new Point2(floor.Boundary.Average(point => point.X), floor.Boundary.Average(point => point.Y));
        if (document.Rooms.FirstOrDefault(item => item.Id == id) is { } room)
            return new Point2((room.FirstCorner.X + room.SecondCorner.X) / 2, (room.FirstCorner.Y + room.SecondCorner.Y) / 2);
        if (document.ElectricalDevices.FirstOrDefault(item => item.Id == id) is { } device) return device.Position;
        if (document.PlumbingPoints.FirstOrDefault(item => item.Id == id) is { } point) return point.Position;
        return null;
    }

    /// <summary>Runs a transformation of the selected element, reporting a refusal in the status line.</summary>
    private bool RunElementTransform(Func<IReadOnlyList<Guid>> transform, string statusKey)
    {
        try
        {
            var made = transform();
            StatusText.Text = GetText(statusKey);
            ReselectPlanElement(made.Count > 0 ? made[^1] : null);
            RefreshAll();
            return true;
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = LocalizedErrors.Translate(exception.Message);
            return false;
        }
    }

    /// <summary>Selects an element by id in whichever list it lives, so the next tool acts on it.</summary>
    private void ReselectPlanElement(Guid? id)
    {
        if (id is not Guid target) return;
        var document = _session.Document;
        if (document.Columns.Any(item => item.Id == target)) SelectExtendedElement(columnId: target);
        else if (document.StructuralElements.Any(item => item.Id == target)) SelectExtendedElement(structuralId: target);
        else if (document.Floors.Any(item => item.Id == target)) SelectExtendedElement(floorId: target);
        else if (document.Rooms.Any(item => item.Id == target)) SelectExtendedElement(roomId: target);
        else if (document.ElectricalDevices.Any(item => item.Id == target)) _selectedElectricalId = target;
        else if (document.PlumbingPoints.Any(item => item.Id == target)) _selectedPlumbingId = target;
    }

    /// <summary>Turns the selected element 90° about its own centre.</summary>
    private bool TryRotateSelectedElement()
    {
        if (SelectedPlanElementId() is not Guid id || ReferencePointOf(id) is not Point2 centre) return false;
        return RunElementTransform(() => _session.TransformPlanElements([id], PlanTransform.Rotation(centre, 90), false, "Transaction.RotateElements"),
            "Status.ElementRotated90");
    }

    /// <summary>Asks for a count and a spacing and copies the selected element along a line.</summary>
    private bool TryArraySelectedElement()
    {
        if (SelectedPlanElementId() is not Guid id) return false;
        var answers = FormDialog.Ask(this, GetText("Array.Title"), GetText("Array.Heading"),
        [
            new(GetText("Array.Count"), "3"), new(GetText("Array.StepX"), "1"), new(GetText("Array.StepY"), "0")
        ]);
        if (answers is null) return true;
        if (!TryReadNumber(answers[0], out var count) || count != Math.Floor(count) || count < 2 || count > 200 ||
            !TryReadNumber(answers[1], out var stepX) || !TryReadNumber(answers[2], out var stepY))
        {
            StatusText.Text = GetText("Status.InvalidNumber");
            return true;
        }
        return RunElementTransform(() =>
        {
            using var group = _session.BeginGroup("Transaction.ArrayElements");
            var made = new List<Guid>();
            for (var index = 1; index < (int)count; index++)
                made.AddRange(_session.TransformPlanElements([id], PlanTransform.Translation(stepX * index, stepY * index), true, "Transaction.ArrayElements"));
            group.Assimilate();
            return made;
        }, "Status.ElementArrayed");
    }

    /// <summary>Picks a wall as the mirror axis and reflects a copy of the selected element across it.</summary>
    private bool TryBeginMirrorSelectedElement()
    {
        if (SelectedPlanElementId() is not Guid id) return false;
        BeginWallPick(axis =>
        {
            var line = _session.Document.Walls.Single(wall => wall.Id == axis);
            _pickWallAction = null;
            return RunElementTransform(() => _session.TransformPlanElements([id], PlanTransform.Reflection(line.Start, line.End), true, "Transaction.MirrorElements"),
                "Status.ElementMirrored");
        }, "Status.PickMirrorAxis");
        return true;
    }

    /// <summary>Remembers the selected element so Paste can drop a copy of it.</summary>
    private bool TryCopySelectedElement()
    {
        if (SelectedPlanElementId() is not Guid id) return false;
        _elementClipboard = [id];
        _wallClipboard = null;
        _openingClipboard = null;
        StatusText.Text = GetText("Status.ElementCopied");
        return true;
    }

    /// <summary>Pastes a copy of the copied element, a metre aside so it does not hide its source.</summary>
    private bool TryPasteElementClipboard()
    {
        if (_elementClipboard is not { Count: > 0 } ids) return false;
        var live = ids.Where(id => ReferencePointOf(id) is not null).ToList();
        if (live.Count == 0) { StatusText.Text = GetText("Status.NothingToPaste"); return true; }
        RunElementTransform(() => _session.TransformPlanElements(live, PlanTransform.Translation(1, 1), true, "Transaction.CopyElements"),
            "Status.ElementPasted");
        return true;
    }
}
