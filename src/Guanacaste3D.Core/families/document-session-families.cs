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

    /// <summary>
    /// Loads a mesh component family (a tree, a car) into the project, replacing an earlier load of the same family so
    /// its instances pick up the new geometry. Returns the family's identifier.
    /// </summary>
    public Guid LoadComponentFamily(FamilyDocument family)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (family.Mesh is null) throw new InvalidDataException("The family has no mesh to place.");
        family.Validate();
        var loaded = family.Clone();
        Commit("Transaction.LoadComponentFamily", document =>
        {
            document.ComponentFamilies.RemoveAll(existing => existing.Id == loaded.Id);
            document.ComponentFamilies.Add(loaded);
        });
        return loaded.Id;
    }

    /// <summary>
    /// Places an instance of a loaded mesh family at its modeled size: a planting for plant families, a component for
    /// the rest. The plan symbol comes from the family's <c>PlanShape</c> metadata (a residential component shape).
    /// </summary>
    public Guid PlaceComponentFamily(Guid levelId, Guid familyId, Point2 at, double rotation = 0, double scale = 1)
    {
        var family = Document.ComponentFamilies.FirstOrDefault(candidate => candidate.Id == familyId)
            ?? throw new InvalidDataException("An element refers to a component family that is not loaded.");
        if (!double.IsFinite(scale) || scale <= 0) throw new InvalidDataException("The family scale must be positive.");
        var size = family.Mesh!.Decode().Size * (float)scale;
        var planting = family.Category == FamilyCategory.Planting;
        var shape = family.Metadata.TryGetValue("PlanShape", out var text) && Enum.TryParse<ResidentialComponentShape>(text, out var parsed)
            ? parsed
            : ResidentialComponentShape.Box;
        var preset = new ElementPreset(family.Name, planting ? StructuralElementKind.Planting : StructuralElementKind.Component,
            ElementStyle.Default, planting ? Math.Max(size.X, size.Y) : size.X, size.Z, 0, planting ? 0 : size.Y,
            planting ? ResidentialComponentShape.Box : shape,
            planting ? ResidentialComponentCategory.None : ResidentialComponentCategory.Outdoor);
        return AddElementFromPreset(levelId, preset, at, at, rotation, familyId);
    }

    /// <summary>
    /// Keeps the Render panel's camera, sun and filters with the project. It is view state, like the camera of the 3D
    /// view, so it does not add an undo step or mark the project as edited.
    /// </summary>
    public void StoreRenderSetup(Rendering.RenderSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        setup.Validate();
        Document.RenderSetup = setup;
    }
}
