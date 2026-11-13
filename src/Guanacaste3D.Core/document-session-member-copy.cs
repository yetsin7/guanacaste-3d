using System.IO;

namespace Guanacaste3D.Core;

public sealed partial class DocumentSession
{
    /// <summary>Copies an independent structural member at a one-meter offset in a single undoable transaction.</summary>
    public Guid DuplicateStructuralMember(Guid elementId)
    {
        var source = Document.StructuralElements.FirstOrDefault(element => element.Id == elementId)
            ?? throw new InvalidDataException("The element does not exist.");
        if (source.Kind is not (StructuralElementKind.Beam or StructuralElementKind.Brace or StructuralElementKind.Truss or
            StructuralElementKind.WallFoundation or StructuralElementKind.IsolatedFoundation))
            throw new InvalidDataException("The selected element cannot be duplicated as a structural member.");
        var copyId = Guid.NewGuid();
        Commit("Transaction.DuplicateStructuralElement", document => document.StructuralElements.Add(source with
        {
            Id = copyId,
            Start = new Point2(source.Start.X + 1, source.Start.Y + 1),
            End = new Point2(source.End.X + 1, source.End.Y + 1),
            GroupId = null,
            RailingHostId = null,
            Cuts = source.Cuts?.ToList()
        }));
        return copyId;
    }
}
