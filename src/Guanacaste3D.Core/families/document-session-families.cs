namespace Guanacaste3D.Core;

/// <summary>Family import as one undoable transaction on a project.</summary>
public sealed partial class DocumentSession
{
    /// <summary>
    /// Adds a family's door or window types to the project catalog. The import is one
    /// transaction: if the merged catalog is invalid, nothing changes.
    /// </summary>
    public void ImportOpeningFamily(OpeningFamilyDocument family) =>
        Commit("Transaction.ImportOpeningFamily", document => OpeningFamilyImport.Apply(document, family));
}
