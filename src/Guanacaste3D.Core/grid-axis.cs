namespace Guanacaste3D.Core;

/// <summary>A named building axis shared by every level; unlike the drafting grid, it is part of the project.</summary>
public sealed record GridAxis(Guid Id, string Name, Point2 Start, Point2 End)
{
    /// <summary>Validates finite metric endpoints and a usable, bounded reference label.</summary>
    public bool IsValid => Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 32 &&
        new SectionCut(Start, End).IsValid;
}

public sealed partial class DocumentSession
{
    /// <summary>Creates a project-wide reference axis in a single undoable transaction.</summary>
    public Guid AddGridAxis(string name, Point2 start, Point2 end)
    {
        var axis = new GridAxis(Guid.NewGuid(), (name ?? string.Empty).Trim(), start, end);
        Commit("Transaction.AddGridAxis", document => document.Grids.Add(axis));
        return axis.Id;
    }

    /// <summary>Renames or moves an axis while preserving associative dimension references.</summary>
    public void UpdateGridAxis(Guid id, string name, Point2 start, Point2 end)
    {
        var index = Document.Grids.FindIndex(axis => axis.Id == id);
        if (index < 0) throw new InvalidDataException("The grid axis does not exist.");
        Commit("Transaction.EditGridAxis", document => document.Grids[index] =
            new(id, (name ?? string.Empty).Trim(), start, end));
    }

    /// <summary>Removes only the datum and its dependent annotations, retaining all building elements.</summary>
    public void RemoveGridAxis(Guid id) =>
        Commit("Transaction.RemoveGridAxis", document => document.Grids.RemoveAll(axis => axis.Id == id));
}
