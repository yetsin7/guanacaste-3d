using System.IO;

namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Copies a column with its section and level as one undoable edit, offset in plan by one meter.</summary>
    public Guid DuplicateColumn(Guid columnId)
    {
        var source = Document.Columns.FirstOrDefault(column => column.Id == columnId)
            ?? throw new InvalidDataException("The column does not exist.");
        var copyId = Guid.NewGuid();
        Commit("Transaction.DuplicateColumn", document => document.Columns.Add(source with
        {
            Id = copyId,
            Position = new Point2(source.Position.X + 1, source.Position.Y + 1)
        }));
        return copyId;
    }
}
