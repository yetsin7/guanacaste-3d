namespace Guanacaste3D.Core;

/// <summary>A named building axis shared by every level; unlike the drafting grid, it is part of the project.</summary>
public sealed record GridAxis(Guid Id, string Name, Point2 Start, Point2 End)
{
    /// <summary>Validates finite metric endpoints and a usable, bounded reference label.</summary>
    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 32 &&
        new SectionCut(Start, End).IsValid;
}

/// <summary>Reports the existing datum so the UI can offer to locate it instead of silently failing.</summary>
public sealed class DuplicateGridAxisNameException : IOException
{
    public Guid ExistingAxisId { get; }
    public string AxisName { get; }

    public DuplicateGridAxisNameException(string axisName, Guid existingAxisId)
        : base($"El eje '{axisName}' ya existe en el proyecto.")
    {
        AxisName = axisName;
        ExistingAxisId = existingAxisId;
    }
}

public sealed partial class DocumentSession
{
    private static string NormalizeAxisName(string? name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (normalized.Length is < 1 or > 32)
            throw new InvalidDataException("The grid axis name must contain between 1 and 32 characters.");
        return normalized;
    }

    private void CheckGridAxisName(string name, Guid editingId = default)
    {
        var duplicate = Document.Grids.FirstOrDefault(axis => axis.Id != editingId &&
            string.Equals(axis.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null) throw new DuplicateGridAxisNameException(name, duplicate.Id);
    }

    /// <summary>Creates a uniquely named project-wide reference axis in a single undoable transaction.</summary>
    public Guid AddGridAxis(string name, Point2 start, Point2 end)
    {
        name = NormalizeAxisName(name);
        CheckGridAxisName(name);
        var axis = new GridAxis(Guid.NewGuid(), name, start, end);
        Commit("Transaction.AddGridAxis", document => document.Grids.Add(axis));
        return axis.Id;
    }

    /// <summary>
    /// Renames or edits an axis. A pure translation also moves collinear walls whose centre lines are aligned
    /// with and overlap the datum. Rotation/reshaping does not silently alter wall geometry.
    /// </summary>
    public void UpdateGridAxis(Guid id, string name, Point2 start, Point2 end)
    {
        var old = Document.Grids.FirstOrDefault(axis => axis.Id == id)
            ?? throw new InvalidDataException("The grid axis does not exist.");
        name = NormalizeAxisName(name);
        CheckGridAxisName(name, id);
        var dx = start.X - old.Start.X;
        var dy = start.Y - old.Start.Y;
        var translation = Math.Abs((old.End.X + dx) - end.X) < 1e-7 &&
                          Math.Abs((old.End.Y + dy) - end.Y) < 1e-7;
        Commit("Transaction.EditGridAxis", document =>
        {
            var index = document.Grids.FindIndex(axis => axis.Id == id);
            document.Grids[index] = new GridAxis(id, name, start, end);
            if (translation && (Math.Abs(dx) > 1e-9 || Math.Abs(dy) > 1e-9))
                TranslateAxisAlignedWalls(document, old, dx, dy);
        });
    }

    /// <summary>Moves an axis and every centre-line-aligned wall in a single undoable model transaction.</summary>
    public void MoveGridAxis(Guid id, double deltaX, double deltaY)
    {
        var axis = Document.Grids.FirstOrDefault(grid => grid.Id == id)
            ?? throw new InvalidDataException("The grid axis does not exist.");
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
            throw new InvalidDataException("The grid axis translation must be finite.");
        if (Math.Abs(deltaX) < 1e-9 && Math.Abs(deltaY) < 1e-9) return;
        UpdateGridAxis(id, axis.Name,
            new Point2(axis.Start.X + deltaX, axis.Start.Y + deltaY),
            new Point2(axis.End.X + deltaX, axis.End.Y + deltaY));
    }

    /// <summary>Finds an axis coinciding with the centerline of the entire wall.</summary>
    private static Guid? FindAlignedGridAxis(BuildingDocument doc, Point2 start, Point2 end) =>
        doc.Grids.FirstOrDefault(axis => IsAligned(axis, start, end))?.Id;

    private static bool IsAligned(GridAxis axis, Point2 start, Point2 end)
    {
        const double toleranceMeters = 0.015;
        var dx = axis.End.X - axis.Start.X;
        var dy = axis.End.Y - axis.Start.Y;
        var length = double.Hypot(dx, dy);
        if (length < 1e-8) return false;
        var ux = dx / length;
        var uy = dy / length;
        var d1 = Math.Abs((start.X - axis.Start.X) * uy - (start.Y - axis.Start.Y) * ux);
        var d2 = Math.Abs((end.X - axis.Start.X) * uy - (end.Y - axis.Start.Y) * ux);
        if (d1 > toleranceMeters || d2 > toleranceMeters) return false;
        var from = (start.X - axis.Start.X) * ux + (start.Y - axis.Start.Y) * uy;
        var to = (end.X - axis.Start.X) * ux + (end.Y - axis.Start.Y) * uy;
        return Math.Max(Math.Min(from, to), 0) < Math.Min(Math.Max(from, to), length) - 1e-8;
    }

    /// <summary>Explicitly binds/unbinds a wall without changing either element's geometry.</summary>
    public void SetWallGridAxis(Guid wallId, Guid? axisId)
    {
        var wall = Document.Walls.FirstOrDefault(w => w.Id == wallId)
            ?? throw new InvalidDataException("The wall does not exist.");
        if (axisId is Guid grid && !Document.Grids.Any(axis => axis.Id == grid))
            throw new InvalidDataException("The grid axis does not exist.");
        if (axisId is Guid selected && !IsAligned(Document.Grids.Single(a => a.Id == selected), wall.Start, wall.End))
            throw new InvalidDataException("The wall centerline must align with the grid axis.");
        Commit("Transaction.BindWallGridAxis", doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == wallId);
            doc.Walls[index] = doc.Walls[index] with
            {
                GridAxisId = axisId,
                GridAxisAssociationDisabled = axisId is null
            };
        });
    }

    /// <summary>Moves only linked walls, plus legacy aligned walls not linked to another axis.</summary>
    private static void TranslateAxisAlignedWalls(BuildingDocument document, GridAxis axis, double dx, double dy)
    {
        for (var i = 0; i < document.Walls.Count; i++)
        {
            var wall = document.Walls[i];
            if (wall.GridAxisId != axis.Id &&
                (wall.GridAxisAssociationDisabled || wall.GridAxisId is not null ||
                 !IsAligned(axis, wall.Start, wall.End))) continue;
            document.Walls[i] = wall with
            {
                Start = new Point2(wall.Start.X + dx, wall.Start.Y + dy),
                End = new Point2(wall.End.X + dx, wall.End.Y + dy),
                GridAxisId = axis.Id,
                GridAxisAssociationDisabled = false
            };
        }
    }

    /// <summary>Removes the datum and dependent annotations, preserving existing building elements.</summary>
    public void RemoveGridAxis(Guid id) =>
        Commit("Transaction.RemoveGridAxis", document =>
        {
            document.Grids.RemoveAll(axis => axis.Id == id);
            for (var i = 0; i < document.Walls.Count; i++)
                if (document.Walls[i].GridAxisId == id)
                    document.Walls[i] = document.Walls[i] with
                    {
                        GridAxisId = null,
                        GridAxisAssociationDisabled = true
                    };
            // Existing walls and their hosted openings are deliberately retained.
        });
}
