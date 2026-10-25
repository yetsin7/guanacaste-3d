namespace Guanacaste3D.Core;

/// <summary>Surface finish assignments; each public method is one validated transaction.</summary>
public sealed partial class DocumentSession
{
    /// <summary>Assigns a finish to a wall's two faces and rejects unknown finish values.</summary>
    public void SetWallFinish(Guid wallId, FinishKind finish)
    {
        if (!Enum.IsDefined(finish))
            throw new InvalidDataException("The selected finish is not supported.");
        if (!Document.Walls.Any(wall => wall.Id == wallId))
            throw new InvalidDataException("The wall does not exist.");
        Commit("Transaction.SetWallFinish", document =>
        {
            var index = document.Walls.FindIndex(wall => wall.Id == wallId);
            document.Walls[index] = document.Walls[index] with { Finish = finish };
        });
    }

    /// <summary>Assigns a finish to a floor's walking surface.</summary>
    public void SetFloorFinish(Guid floorId, FinishKind finish)
    {
        if (!Enum.IsDefined(finish))
            throw new InvalidDataException("The selected finish is not supported.");
        if (!Document.Floors.Any(floor => floor.Id == floorId))
            throw new InvalidDataException("The floor slab does not exist.");
        Commit("Transaction.SetFloorFinish", document =>
        {
            var index = document.Floors.FindIndex(floor => floor.Id == floorId);
            document.Floors[index] = document.Floors[index] with { Finish = finish };
        });
    }
}
