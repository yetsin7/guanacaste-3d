using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Exercises beam duplication through the ribbon, including selection and undo history.</summary>
    private static void DuplicateStructuralMemberChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var source = session.AddStructuralElement(level, StructuralElementKind.Beam, "Test beam",
            new Point2(390, 390), new Point2(395, 390));
        Guid? copy = null;
        try
        {
            SetField("_selectedWallId", null);
            SetField("_selectedFloorId", null);
            SetField("_selectedColumnId", null);
            SetField("_selectedStructuralId", (Guid?)source);
            Invoke("ExecuteApplicationCommand", "CopyElement");
            copy = Field<Guid?>("_selectedStructuralId");
            var original = session.Document.StructuralElements.Single(element => element.Id == source);
            var duplicate = session.Document.StructuralElements.Single(element => element.Id == copy);
            Check(copy != source && duplicate.Start == new Point2(original.Start.X + 1, original.Start.Y + 1) &&
                duplicate.End == new Point2(original.End.X + 1, original.End.Y + 1) && duplicate.Width == original.Width &&
                duplicate.Height == original.Height && duplicate.LevelId == original.LevelId && duplicate.GroupId is null,
                "Copy Element duplicates the beam geometry and selects an independent member");
            session.Undo();
            Check(session.Document.StructuralElements.All(element => element.Id != copy), "Undo removes the copied beam");
            session.Redo();
            Check(session.Document.StructuralElements.Any(element => element.Id == copy), "Redo restores the copied beam");
        }
        finally
        {
            SetField("_selectedStructuralId", null);
            if (copy is Guid copyId) session.RemoveStructuralElement(copyId);
            session.RemoveStructuralElement(source);
        }
    }
}
