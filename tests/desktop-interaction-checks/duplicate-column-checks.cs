using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Verifies that the ribbon duplicates a column without losing its section and supports undo.</summary>
    private static void DuplicateColumnRibbonChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var source = session.AddColumnFromPreset(level, new Point2(370, 370),
            new ColumnPreset("Test column", ColumnShape.Round, 0.7, 0.7), 4.2);
        Guid? copy = null;
        try
        {
            SetField("_selectedWallId", null);
            SetField("_selectedStructuralId", null);
            SetField("_selectedColumnId", (Guid?)source);
            Invoke("ExecuteApplicationCommand", "CopyElement");
            copy = Field<Guid?>("_selectedColumnId");
            var original = session.Document.Columns.Single(column => column.Id == source);
            var duplicate = session.Document.Columns.Single(column => column.Id == copy);
            Check(copy != source && duplicate == (original with
            {
                Id = copy!.Value,
                Position = new Point2(original.Position.X + 1, original.Position.Y + 1)
            }), "Copy Element preserves the column section and selects its offset copy");
            session.Undo();
            Check(session.Document.Columns.All(column => column.Id != copy), "Undo removes the column copy");
            session.Redo();
            Check(session.Document.Columns.Any(column => column.Id == copy), "Redo restores the column copy");
        }
        finally
        {
            SetField("_selectedColumnId", null);
            if (copy is Guid copyId) session.RemoveColumn(copyId);
            session.RemoveColumn(source);
        }
    }
}
