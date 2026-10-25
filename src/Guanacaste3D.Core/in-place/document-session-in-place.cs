namespace Guanacaste3D.Core;

/// <summary>
/// In-place family editing. Each public method is one named transaction, so every change can be
/// undone, and a change that fails validation leaves the document untouched.
/// </summary>
public sealed partial class DocumentSession
{
    /// <summary>Creates an empty in-place family in a category that accepts in-place families.</summary>
    public Guid CreateInPlaceFamily(string categoryId, string name)
    {
        var category = BimCategoryCatalog.Find(categoryId)
            ?? throw new InvalidDataException("The family category does not exist.");
        if (!category.SupportsInPlace)
            throw new InvalidDataException("This category does not accept in-place families yet.");
        var id = Guid.NewGuid();
        Commit("Transaction.CreateInPlaceFamily", document => document.InPlaceFamilies.Add(
            new InPlaceFamily(id, name.Trim(), category.Id, [], [])));
        return id;
    }

    /// <summary>Renames an in-place family. Names stay unique within the document.</summary>
    public void RenameInPlaceFamily(Guid familyId, string name)
    {
        RequireFamily(familyId);
        Commit("Transaction.RenameInPlaceFamily", document =>
        {
            var index = FamilyIndex(document, familyId);
            document.InPlaceFamilies[index] = document.InPlaceFamilies[index] with { Name = name.Trim() };
        });
    }

    /// <summary>Adds a length parameter to a family, or updates an existing one (meters), matched case-insensitively.</summary>
    public void SetInPlaceParameter(Guid familyId, string name, double value)
    {
        RequireFamily(familyId);
        var trimmed = name.Trim();
        Commit("Transaction.SetInPlaceParameter", document =>
        {
            var parameters = document.InPlaceFamilies[FamilyIndex(document, familyId)].Parameters;
            var existing = parameters.FindIndex(parameter =>
                string.Equals(parameter.Name, trimmed, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) parameters[existing] = parameters[existing] with { Value = value };
            else parameters.Add(new InPlaceParameter(trimmed, value));
        });
    }

    /// <summary>
    /// Adds an extrusion of a closed profile along the work-plane normal. When
    /// <paramref name="depthParameter"/> is set, the depth follows that family parameter.
    /// </summary>
    public Guid AddInPlaceExtrusion(Guid familyId, WorkPlane plane, IReadOnlyList<Point2> profile,
        double startOffset, double depth, string? depthParameter = null, string materialName = "",
        string? name = null)
    {
        RequireFamily(familyId);
        var id = Guid.NewGuid();
        Commit("Transaction.AddInPlaceExtrusion", document =>
        {
            var family = document.InPlaceFamilies[FamilyIndex(document, familyId)];
            var label = name?.Trim() is { Length: > 0 } trimmed ? trimmed : $"Extrusion {family.Extrusions.Count + 1}";
            family.Extrusions.Add(new InPlaceExtrusion(id, label, plane, [.. profile], startOffset, depth)
            {
                DepthParameter = depthParameter?.Trim(),
                MaterialName = materialName.Trim()
            });
        });
        return id;
    }

    /// <summary>
    /// Replaces the geometry definition of one extrusion while keeping its identifier and name, so
    /// the solid is regenerated in place rather than deleted and recreated.
    /// </summary>
    public void UpdateInPlaceExtrusion(Guid familyId, Guid extrusionId, WorkPlane plane,
        IReadOnlyList<Point2> profile, double startOffset, double depth, string? depthParameter,
        string materialName)
    {
        RequireExtrusion(familyId, extrusionId);
        Commit("Transaction.UpdateInPlaceExtrusion", document =>
        {
            var extrusions = document.InPlaceFamilies[FamilyIndex(document, familyId)].Extrusions;
            var index = extrusions.FindIndex(extrusion => extrusion.Id == extrusionId);
            extrusions[index] = extrusions[index] with
            {
                Plane = plane,
                Profile = [.. profile],
                StartOffset = startOffset,
                Depth = depth,
                DepthParameter = depthParameter?.Trim(),
                MaterialName = materialName.Trim()
            };
        });
    }

    /// <summary>Removes one extrusion from a family; the family itself stays.</summary>
    public void RemoveInPlaceExtrusion(Guid familyId, Guid extrusionId)
    {
        if (!Document.InPlaceFamilies.Any(family => family.Id == familyId &&
                family.Extrusions.Any(extrusion => extrusion.Id == extrusionId)))
            return;
        Commit("Transaction.RemoveInPlaceExtrusion", document =>
            document.InPlaceFamilies[FamilyIndex(document, familyId)].Extrusions
                .RemoveAll(extrusion => extrusion.Id == extrusionId));
    }

    /// <summary>Removes an in-place family and all of its operations.</summary>
    public void RemoveInPlaceFamily(Guid familyId)
    {
        if (!Document.InPlaceFamilies.Any(family => family.Id == familyId)) return;
        Commit("Transaction.RemoveInPlaceFamily", document =>
            document.InPlaceFamilies.RemoveAll(family => family.Id == familyId));
    }

    /// <summary>Throws when the family does not exist in the current document.</summary>
    private void RequireFamily(Guid familyId)
    {
        if (!Document.InPlaceFamilies.Any(family => family.Id == familyId))
            throw new InvalidDataException("The in-place family does not exist.");
    }

    /// <summary>Throws when the family or the extrusion inside it does not exist.</summary>
    private void RequireExtrusion(Guid familyId, Guid extrusionId)
    {
        RequireFamily(familyId);
        if (!Document.InPlaceFamilies.Single(family => family.Id == familyId).Extrusions
                .Any(extrusion => extrusion.Id == extrusionId))
            throw new InvalidDataException("The extrusion does not exist.");
    }

    /// <summary>Returns the index of a family inside a document copy, or throws when it is missing.</summary>
    private static int FamilyIndex(BuildingDocument document, Guid familyId)
    {
        var index = document.InPlaceFamilies.FindIndex(family => family.Id == familyId);
        return index >= 0 ? index : throw new InvalidDataException("The in-place family does not exist.");
    }
}
