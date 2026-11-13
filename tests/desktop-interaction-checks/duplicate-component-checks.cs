using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Exercises component duplication through the shared ribbon command, including undo and redo.</summary>
    private static void DuplicateComponentRibbonChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var source = session.AddStructuralElement(level, StructuralElementKind.Component, "Test component",
            new Point2(350, 350), new Point2(350, 350));
        Guid? copy = null;
        try
        {
            SetField("_selectedWallId", null);
            SetField("_selectedColumnId", null);
            SetField("_selectedStructuralId", (Guid?)source);
            Invoke("ExecuteApplicationCommand", "CopyElement");
            copy = Field<Guid?>("_selectedStructuralId");
            var original = session.Document.StructuralElements.Single(element => element.Id == source);
            var duplicate = session.Document.StructuralElements.Single(element => element.Id == copy);
            Check(copy != source && duplicate.Start == new Point2(original.Start.X + 1, original.Start.Y + 1),
                "The Copy Element ribbon command duplicates and selects a component with a visible offset");
            session.Undo();
            Check(session.Document.StructuralElements.All(element => element.Id != copy),
                "Undo removes the component copy");
            session.Redo();
            Check(session.Document.StructuralElements.Any(element => element.Id == copy),
                "Redo restores the same component copy");
        }
        finally
        {
            SetField("_selectedStructuralId", null);
            if (copy is Guid copyId && session.Document.StructuralElements.Any(element => element.Id == copyId))
                session.RemoveStructuralElement(copyId);
            if (session.Document.StructuralElements.Any(element => element.Id == source))
                session.RemoveStructuralElement(source);
        }
    }
}
