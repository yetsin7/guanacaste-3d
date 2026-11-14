using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;

/// <summary>Load schedule, plumbing schedule, symbol legend and budget placed on sheets as live tables.</summary>
static class EngineeringTableChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Tablas de ingeniería"));
        var level = session.Document.Levels[0].Id;
        session.AddWallsAroundBoundary(level, [new(0, 0), new(6, 0), new(6, 4), new(0, 4)], BuiltInWallTypes.Basic200Id);
        var panel = session.AddElectricalDevice(level, ElectricalDeviceKind.Panel, "TP-1", new Point2(0.2, 2));
        var outlet = session.AddElectricalDevice(level, ElectricalDeviceKind.Outlet, "T1", new Point2(3, 0.2));
        var circuit = session.AddCircuit(panel, "Tomas sala");
        session.AssignCircuit(outlet, circuit);

        foreach (var id in new[] { EngineeringTables.PanelScheduleId, EngineeringTables.PlumbingScheduleId, EngineeringTables.SymbolLegendId, EngineeringTables.BudgetTableId })
            check(session.Document.Views.Count(view => view.Id == id) == 1, $"Tables: a new project lists the generated table {id}");

        var sheetId = session.AddSheet(SheetPaperSize.A1, "Eléctrica");
        var sheet = session.Document.Sheets.Single(candidate => candidate.Id == sheetId);
        var area = sheet.Viewports[0];
        SheetViewport Place(Guid view, double top, string number) =>
            new(Guid.NewGuid(), view, area.X, area.Y + top * area.Height, area.Width * 0.6, area.Height * 0.24, 0, number);
        session.UpdateSheet(sheet with
        {
            Viewports = [Place(EngineeringTables.PanelScheduleId, 0, "1"), Place(EngineeringTables.SymbolLegendId, 0.25, "2"),
                Place(EngineeringTables.BudgetTableId, 0.5, "3"), Place(EngineeringTables.PlumbingScheduleId, 0.75, "4")]
        });
        var composed = SheetComposition.Build(session.Document, session.Document.Sheets.Single(candidate => candidate.Id == sheetId), DateTimeOffset.Now);
        var texts = composed.Viewports.SelectMany(viewport => viewport.Graphics).OfType<SheetText>().Select(text => text.Text).ToList();
        check(texts.Contains("TP-1") && texts.Contains("C1") && texts.Contains("Tomas sala") && texts.Contains("Total del tablero"),
            "Tables: the load schedule on a sheet lists the panel, its circuit and the panel total");
        check(texts.Any(text => text.EndsWith("AWG (2.1 mm²)", StringComparison.Ordinal) || text.Contains("AWG", StringComparison.Ordinal)) &&
              texts.Any(text => text.EndsWith(" %", StringComparison.Ordinal)),
            "Tables: each circuit shows its conductor and voltage drop");
        check(texts.Contains("Tomacorriente doble 120 V") && texts.Contains("Aguas negras (con pendiente)") &&
              composed.Viewports.Single(viewport => viewport.Placement.Number == "2").Graphics.OfType<SheetPolygon>().Any(),
            "Tables: the symbol legend draws the plan symbols with their meaning");
        check(texts.Contains("TOTAL") && texts.Contains("Mampostería"), "Tables: the budget table shows its trades and total");
        check(texts.Any(text => text.Contains("conecte la plomería", StringComparison.Ordinal)),
            "Tables: the plumbing schedule says what to do when there is no system yet");

        // A project saved before the tables existed gets them when it is opened.
        var old = session.Document.Clone();
        old.Views.RemoveAll(view => EngineeringTables.IsEngineeringTable(view.Id));
        var reopened = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(old));
        check(reopened.Views.Count(view => EngineeringTables.IsEngineeringTable(view.Id)) == 4, "Tables: an older project receives the tables when it opens");
    }
}
