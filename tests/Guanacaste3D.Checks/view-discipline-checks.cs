using Guanacaste3D.Core;

/// <summary>Duplicated plans and view disciplines: the base of electrical, plumbing, structural and mechanical plans.</summary>
static class ViewDisciplineChecks
{
    public static void Run(Action<bool, string> check)
    {
        var session = new DocumentSession(BuildingDocument.Create("Disciplinas"));
        var level = session.Document.Levels[0].Id;
        var plan = session.Document.Views.First(view => view.Kind == ProjectViewKind.Plan && view.LevelId == level);
        session.UpdatePlanViewRange(plan.Id, new PlanViewRange(0.8, 1, 2.6));
        var copy = session.DuplicateView(plan.Id);
        var duplicated = session.Document.Views.Single(view => view.Id == copy);
        check(duplicated.Kind == ProjectViewKind.Plan && duplicated.LevelId == level && duplicated.Range == new PlanViewRange(0.8, 1, 2.6) &&
              duplicated.Name == plan.Name + " copia 1" && session.DuplicateView(plan.Id) != copy &&
              session.Document.Views.Any(view => view.Name == plan.Name + " copia 2"),
            "Views: a plan duplicates with its level and range under the next free «copia N» name");
        session.SetViewDiscipline(copy, ViewDiscipline.Electrical);
        check(session.Document.Views.Single(view => view.Id == copy).Discipline == ViewDiscipline.Electrical &&
              session.Document.Views.Single(view => view.Id == plan.Id).Discipline == ViewDiscipline.Architectural,
            "Views: a duplicated plan takes its own discipline without changing the original");
        session.Undo();
        check(session.Document.Views.Single(view => view.Id == copy).Discipline == ViewDiscipline.Architectural,
            "Views: changing the discipline is one undoable step");
        var refused = false;
        try { session.DuplicateView(BuiltInProjectViews.Model3DId); }
        catch (InvalidDataException) { refused = true; }
        check(refused, "Views: only floor plans can be duplicated for now");

        var wall = session.AddWall(level, new Point2(0, 0), new Point2(4, 0));
        var column = session.AddColumn(level, new Point2(5, 5));
        var footing = session.AddStructuralElement(level, StructuralElementKind.IsolatedFoundation, "Zapata", new Point2(5, 5), new Point2(5, 5));
        var duct = session.AddStructuralElement(level, StructuralElementKind.Duct, "Ducto", new Point2(0, 1), new Point2(3, 1));
        var document = session.Document;
        check(ViewDisciplines.Of(document, wall) == ViewDiscipline.Architectural && ViewDisciplines.Of(document, column) == ViewDiscipline.Structural &&
              ViewDisciplines.Of(document, footing) == ViewDiscipline.Structural && ViewDisciplines.Of(document, duct) == ViewDiscipline.Mechanical &&
              ViewDisciplines.Of(document, Guid.NewGuid()) is null,
            "Views: walls are architectural, columns and footings structural, ducts mechanical; unknown ids have none");
        check(ViewDisciplines.IsHalftone(ViewDiscipline.Electrical, ViewDiscipline.Architectural) &&
              !ViewDisciplines.IsHalftone(ViewDiscipline.Electrical, ViewDiscipline.Electrical) &&
              !ViewDisciplines.IsHalftone(ViewDiscipline.Architectural, ViewDiscipline.Structural) &&
              !ViewDisciplines.IsHalftone(ViewDiscipline.Coordination, ViewDiscipline.Plumbing),
            "Views: a discipline view dims the other disciplines; architectural and coordination views dim nothing");
        session.AddElectricalDevice(level, ElectricalDeviceKind.Outlet, "T1", new Point2(1, 0.2));
        var architectural = Guanacaste3D.Core.Interop.PdfPlanExporter.PlanPrimitives(session.Document, level, ViewDiscipline.Architectural);
        var electrical = Guanacaste3D.Core.Interop.PdfPlanExporter.PlanPrimitives(session.Document, level, ViewDiscipline.Electrical);
        static bool Red(Guanacaste3D.Core.Interop.SheetPrimitive primitive) => primitive.Color.R > 0.6 && primitive.Color.G < 0.3;
        check(!architectural.Any(Red) && electrical.Any(Red),
            "Sheets: an electrical plan prints its devices; the architectural plan of the same level does not");
        var wallInk = electrical.OfType<Guanacaste3D.Core.Interop.SheetPolygon>().Where(polygon => !Red(polygon)).Select(polygon => polygon.Color.R).DefaultIfEmpty(1).Min();
        check(wallInk > 0.5, "Sheets: an electrical plan prints the architecture halftone");
        var saved = ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(document));
        check(saved.Views.Count == document.Views.Count, "Views: duplicated plans survive saving");
    }
}
