using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;

/// <summary>Checks the area table: grouping by level, totals, and placement on a sheet.</summary>
static class AreaScheduleChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Areas"));
        var ground = session.Document.Levels[0].Id;
        var upper = session.Document.Levels.Single(level => level.Role == LevelRole.BaseRoof).Id;
        session.AddRoom(ground, new Point2(0, 0), new Point2(4, 3), "Sala");
        session.AddRoom(ground, new Point2(4, 0), new Point2(6, 3), "Cocina");
        session.AddRoom(upper, new Point2(0, 0), new Point2(3, 3), "Dormitorio");

        var rows = AreaSchedule.Rows(session.Document);
        check(rows.Count(row => row.Kind == AreaScheduleRowKind.Room) == 3, "The area table lists every room");
        check(rows.Where(row => row.Kind == AreaScheduleRowKind.Subtotal).Select(row => row.Area).SequenceEqual([18.0, 9.0]),
            "The area table subtotals each level");
        check(Math.Abs(rows.Last().Area - 27) < 1e-9 && rows.Last().Kind == AreaScheduleRowKind.Total,
            "The area table ends with the grand total");
        check(AreaSchedule.Rows(new DocumentSession(BuildingDocument.Create("Empty")).Document).Count == 0,
            "A project without rooms has an empty area table");

        check(session.Document.Views.Any(view => view.Id == BuiltInProjectViews.AreaScheduleId),
            "New projects include the area table view");

        var sheetId = session.AddSheet();
        var sheet = session.Document.Sheets.Single(candidate => candidate.Id == sheetId);
        var layout = session.Document.SheetFamilies.Single(family => family.Id == sheet.TitleBlockFamilyId).SheetLayout!;
        var viewport = new SheetViewport(Guid.NewGuid(), BuiltInProjectViews.AreaScheduleId, layout.Margin + 5,
            layout.Margin + layout.HeaderHeight + 5, 120, 80);
        session.UpdateSheet(sheet with { Viewports = [viewport] });
        var composed = SheetComposition.Build(session.Document, session.Document.Sheets.Single(), DateTimeOffset.Now);
        var texts = composed.Viewports.Single().Graphics.OfType<SheetText>().Select(text => text.Text).ToList();
        check(texts.Contains("Sala") && texts.Contains(AreaSchedule.Format(27)),
            "A sheet draws the area table with room names and the total");
        check(PdfPlanExporter.ExportSheets(session.Document, DateTimeOffset.Now).Length > 0,
            "A sheet carrying the area table exports to PDF");
    }
}
