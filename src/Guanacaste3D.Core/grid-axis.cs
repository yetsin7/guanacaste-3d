namespace Guanacaste3D.Core;

/// <summary>A named building datum shared by every level, independent of the drawing background grid.</summary>
public sealed record GridAxis(Guid Id, string Name, Point2 Start, Point2 End)
{
    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 32 &&
        new SectionCut(Start, End).IsValid;
}

/// <summary>Identifies the existing datum so the UI can select it when a name is reused.</summary>
public sealed class GridAxisNameConflictException : InvalidDataException
{
    public Guid ExistingAxisId { get; }
    public string AxisName { get; }

    public GridAxisNameConflictException(Guid existingAxisId, string name)
        : base($"An axis named '{name}' already exists.")
    {
        ExistingAxisId = existingAxisId;
        AxisName = name;
    }
}

/// <summary>Parametric grid operations, independent of the WPF interaction used to drag a datum.</summary>
public sealed partial class DocumentSession
{
    private const double GridWallCenterlineTolerance = 0.005; // 5 mm drafting tolerance

    /// <summary>Finds exactly one axis collinear with the wall centerline; intersecting axes never bind a wall.</summary>
    private static Guid? MatchingWallGrid(IEnumerable<GridAxis> axes, Point2 start, Point2 end)
    {
        var wallLength = start.DistanceTo(end);
        if (wallLength < 0.05) return null;
        Guid? match = null;
        foreach (var axis in axes)
        {
            var dx = axis.End.X - axis.Start.X;
            var dy = axis.End.Y - axis.Start.Y;
            var axisLength = Math.Hypot(dx, dy);
            if (axisLength < 1e-6) continue;
            var wx = end.X - start.X;
            var wy = end.Y - start.Y;
            var parallel = Math.Abs(dx * wy - dy * wx) / (axisLength * wallLength);
            var startDistance = Math.Abs(dx * (start.Y - axis.Start.Y) - dy * (start.X - axis.Start.X)) / axisLength;
            var endDistance = Math.Abs(dx * (end.Y - axis.Start.Y) - dy * (end.X - axis.Start.X)) / axisLength;
            if (parallel > 1e-5 || startDistance > GridWallCenterlineTolerance ||
                endDistance > GridWallCenterlineTolerance) continue;
            if (match is not null) return null; // Coincident multiple axes: require explicit user choice.
            match = axis.Id;
        }
        return match;
    }

    private static string NormalizeGridName(string? name) => (name ?? string.Empty).Trim();

    private static void EnsureUniqueGridName(BuildingDocument document, Guid editedId, string name)
    {
        var duplicate = document.Grids.FirstOrDefault(axis => axis.Id != editedId &&
            string.Equals(axis.Name, name, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null) throw new GridAxisNameConflictException(duplicate.Id, duplicate.Name);
    }

    /// <summary>Creates an axis and links already centered, unbound walls without changing their geometry.</summary>
    public Guid AddGridAxis(string name, Point2 start, Point2 end)
    {
        var axis = new GridAxis(Guid.NewGuid(), NormalizeGridName(name), start, end);
        EnsureUniqueGridName(Document, axis.Id, axis.Name);
        Commit("Transaction.AddGridAxis", document =>
        {
            document.Grids.Add(axis);
            for (var i = 0; i < document.Walls.Count; i++)
            {
                var wall = document.Walls[i];
                if (wall.GridAxisId is null && MatchingWallGrid(document.Grids, wall.Start, wall.End) == axis.Id)
                    document.Walls[i] = wall with { GridAxisId = axis.Id };
            }
        });
        return axis.Id;
    }

    /// <summary>Links one coincident wall to one existing grid without deleting or moving either element.</summary>
    public void AttachWallToGridAxis(Guid wallId, Guid axisId)
    {
        var wall = Document.Walls.FirstOrDefault(item => item.Id == wallId)
            ?? throw new InvalidDataException("The wall does not exist.");
        var axis = Document.Grids.FirstOrDefault(item => item.Id == axisId)
            ?? throw new InvalidDataException("The grid axis does not exist.");
        if (MatchingWallGrid([axis], wall.Start, wall.End) != axisId)
            throw new InvalidDataException("The wall centerline is not aligned with the selected grid.");
        Commit("Transaction.AttachWallToGrid", document =>
        {
            var index = document.Walls.FindIndex(item => item.Id == wallId);
            document.Walls[index] = document.Walls[index] with { GridAxisId = axisId };
        });
    }

    /// <summary>Detaches a wall from its grid without changing either element's geometry.</summary>
    public void DetachWallFromGridAxis(Guid wallId)
    {
        if (!Document.Walls.Any(item => item.Id == wallId))
            throw new InvalidDataException("The wall does not exist.");
        Commit("Transaction.DetachWallFromGrid", document =>
        {
            var index = document.Walls.FindIndex(item => item.Id == wallId);
            document.Walls[index] = document.Walls[index] with { GridAxisId = null };
        });
    }

    /// <summary>
    /// Renames or edits a datum. Attached walls follow its rigid planar change while keeping their
    /// own lengths, thicknesses and hosted openings. Moving only an endpoint rotates the axis
    /// rather than stretching wall geometry. All changes are one undoable transaction.
    /// </summary>
    public void UpdateGridAxis(Guid id, string name, Point2 start, Point2 end)
    {
        var before = Document.Grids.FirstOrDefault(axis => axis.Id == id)
            ?? throw new InvalidDataException("The grid axis does not exist.");
        name = NormalizeGridName(name);
        EnsureUniqueGridName(Document, id, name);
        var after = new GridAxis(id, name, start, end);
        Commit("Transaction.EditGridAxis", document =>
        {
            var index = document.Grids.FindIndex(axis => axis.Id == id);
            document.Grids[index] = after;
            if (before.Start == start && before.End == end) return;

            var oldX = before.End.X - before.Start.X;
            var oldY = before.End.Y - before.Start.Y;
            var newX = end.X - start.X;
            var newY = end.Y - start.Y;
            var oldLength = Math.Hypot(oldX, oldY);
            var newLength = Math.Hypot(newX, newY);
            if (oldLength < 1e-8 || newLength < 1e-8)
                throw new InvalidDataException("The grid axis is too short.");
            var oux = oldX / oldLength;
            var ouy = oldY / oldLength;
            var nux = newX / newLength;
            var nuy = newY / newLength;
            Point2 Transform(Point2 point)
            {
                var dx = point.X - before.Start.X;
                var dy = point.Y - before.Start.Y;
                var along = dx * oux + dy * ouy;
                var across = -dx * ouy + dy * oux;
                return new(start.X + along * nux - across * nuy,
                    start.Y + along * nuy + across * nux);
            }
            for (var wallIndex = 0; wallIndex < document.Walls.Count; wallIndex++)
            {
                var wall = document.Walls[wallIndex];
                if (wall.GridAxisId == id)
                    document.Walls[wallIndex] = wall with { Start = Transform(wall.Start), End = Transform(wall.End) };
            }
        });
    }

    /// <summary>Moves the reference and its attached walls together, including hosted openings.</summary>
    public void MoveGridAxis(Guid id, double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
            throw new InvalidDataException("The grid movement must be finite.");
        var axis = Document.Grids.FirstOrDefault(item => item.Id == id)
            ?? throw new InvalidDataException("The grid axis does not exist.");
        UpdateGridAxis(id, axis.Name,
            new(axis.Start.X + deltaX, axis.Start.Y + deltaY),
            new(axis.End.X + deltaX, axis.End.Y + deltaY));
    }

    /// <summary>Removes only the datum, detaching walls and removing dependent dimensions.</summary>
    public void RemoveGridAxis(Guid id)
    {
        if (!Document.Grids.Any(axis => axis.Id == id)) return;
        Commit("Transaction.RemoveGridAxis", document =>
        {
            document.Grids.RemoveAll(axis => axis.Id == id);
            document.Dimensions.RemoveAll(dimension => dimension.OwnerId == id || dimension.ReferenceId == id);
            for (var i = 0; i < document.Walls.Count; i++)
                if (document.Walls[i].GridAxisId == id)
                    document.Walls[i] = document.Walls[i] with { GridAxisId = null };
        });
    }
}
