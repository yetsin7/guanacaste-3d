namespace Guanacaste3D.Core;

/// <summary>A named building axis shared by every level; unlike the drafting grid, it is part of the project.</summary>
public sealed record GridAxis(Guid Id, string Name, Point2 Start, Point2 End)
{
    /// <summary>Validates finite metric endpoints and a usable, bounded reference label.</summary>
    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 32 &&
        new SectionCut(Start, End).IsValid;
}

/// <summary>Reports the existing datum so the UI can offer to locate it instead of silently failing.</summary>
public sealed class DuplicateGridAxisNameException : InvalidDataException
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
            throw new InvalidDataException("El nombre del eje debe contener entre 1 y 32 caracteres.");
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
            ?? throw new InvalidDataException("El eje seleccionado ya no existe.");
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
            ?? throw new InvalidDataException("El eje seleccionado ya no existe.");
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
            throw new InvalidDataException("El desplazamiento del eje debe ser finito.");
        if (Math.Abs(deltaX) < 1e-9 && Math.Abs(deltaY) < 1e-9) return;
        UpdateGridAxis(id, axis.Name,
            new Point2(axis.Start.X + deltaX, axis.Start.Y + deltaY),
            new Point2(axis.End.X + deltaX, axis.End.Y + deltaY));
    }

    // A wall belongs to an axis geometrically only if the whole wall centre line sits on that datum
    // and both finite segments overlap. It is not enough to merely cross the axis.
    private static void TranslateAxisAlignedWalls(BuildingDocument document, GridAxis axis, double dx, double dy)
    {
        const double toleranceMeters = 0.015;
        var axisDx = axis.End.X - axis.Start.X;
        var axisDy = axis.End.Y - axis.Start.Y;
        var axisLength = Math.Hypot(axisDx, axisDy);
        var ux = axisDx / axisLength;
        var uy = axisDy / axisLength;

        for (var i = 0; i < document.Walls.Count; i++)
        {
            var wall = document.Walls[i];
            var distanceStart = Math.Abs((wall.Start.X - axis.Start.X) * uy -
                                         (wall.Start.Y - axis.Start.Y) * ux);
            var distanceEnd = Math.Abs((wall.End.X - axis.Start.X) * uy -
                                       (wall.End.Y - axis.Start.Y) * ux);
            if (distanceStart > toleranceMeters || distanceEnd > toleranceMeters) continue;
            var from = (wall.Start.X - axis.Start.X) * ux + (wall.Start.Y - axis.Start.Y) * uy;
            var to = (wall.End.X - axis.Start.X) * ux + (wall.End.Y - axis.Start.Y) * uy;
            if (Math.Max(Math.Min(from, to), 0) >= Math.Min(Math.Max(from, to), axisLength) - 1e-8) continue;

            document.Walls[i] = wall with
            {
                Start = new Point2(wall.Start.X + dx, wall.Start.Y + dy),
                End = new Point2(wall.End.X + dx, wall.End.Y + dy)
            };
            // Wall-hosted openings use the wall ID and local offset: they follow the moved host automatically.
        }
    }

    /// <summary>Removes the datum and dependent annotations, preserving existing building elements.</summary>
    public void RemoveGridAxis(Guid id) =>
        Commit("Transaction.RemoveGridAxis", document => document.Grids.RemoveAll(axis => axis.Id == id));
}
