using System.IO;

namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Copies a slab or terrain with its geometry and level, offset one meter in plan in one undoable transaction.</summary>
    public Guid DuplicateFloor(Guid floorId)
    {
        var source = Document.Floors.FirstOrDefault(floor => floor.Id == floorId)
            ?? throw new InvalidDataException("The floor slab does not exist.");
        var copyId = Guid.NewGuid();
        Commit("Transaction.DuplicateFloor", document => document.Floors.Add(source with
        {
            Id = copyId,
            Boundary = source.Boundary.Select(point => new Point2(point.X + 1, point.Y + 1)).ToList(),
            TopoPoints = source.TopoPoints?.Select(point => point with { X = point.X + 1, Y = point.Y + 1 }).ToList()
        }));
        return copyId;
    }
}
