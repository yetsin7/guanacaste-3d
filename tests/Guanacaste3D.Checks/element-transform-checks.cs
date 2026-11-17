using Guanacaste3D.Core;

/// <summary>Rotate, mirror and copy of columns, furniture, slabs, rooms and devices through one transformation.</summary>
static class ElementTransformChecks
{
    public static void Run(Action<bool, string> check)
    {
        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
        var session = new DocumentSession(BuildingDocument.Create("Transform"));
        var level = session.Document.Levels[0].Id;
        var column = session.AddColumn(level, new(2, 1));
        session.UpdateColumn(column, 0.4, 0.2, 3, ColumnShape.Rectangular);

        session.TransformPlanElements([column], PlanTransform.Rotation(new(2, 1), 90), false, "Transaction.RotateElements");
        var turned = session.Document.Columns.Single(item => item.Id == column);
        check(Near(turned.Width, 0.2) && Near(turned.Depth, 0.4) && Near(turned.Position.X, 2) && Near(turned.Position.Y, 1),
            "Transform: a quarter turn about a column's own centre swaps its sides and keeps its place");
        session.Undo();
        check(Near(session.Document.Columns.Single(item => item.Id == column).Width, 0.4), "Transform: a rotation is one undoable step");

        var copy = session.TransformPlanElements([column], PlanTransform.Reflection(new(5, 0), new(5, 10)), true, "Transaction.MirrorElements");
        check(copy.Count == 1 && session.Document.Columns.Count == 2 && Near(session.Document.Columns.Single(item => item.Id == copy[0]).Position.X, 8),
            "Transform: reflecting across x = 5 puts a copy at x = 8 and keeps the original");

        var floor = session.AddFloor(level, [new(0, 0), new(2, 0), new(2, 1), new(0, 1)], 0.1);
        var moved = session.TransformPlanElements([floor], PlanTransform.Translation(10, 0), true, "Transaction.CopyElements");
        check(session.Document.Floors.Count == 2 && session.Document.Floors.Single(item => item.Id == moved[0]).Boundary.All(point => point.X >= 10),
            "Transform: a copied slab lands on its new place");

        var device = session.AddElectricalDevice(level, ElectricalDeviceKind.Outlet, "T1", new(0.2, 2));
        session.TransformPlanElements([device], PlanTransform.Rotation(new(0, 0), 90), false, "Transaction.RotateElements");
        check(Near(session.Document.ElectricalDevices.Single(item => item.Id == device).Position.X, -2),
            "Transform: electrical devices turn about the centre too");

        var failed = false;
        try { session.TransformPlanElements([Guid.NewGuid()], PlanTransform.Translation(1, 1), false, "Transaction.CopyElements"); }
        catch (InvalidDataException) { failed = true; }
        check(failed, "Transform: an id that is not an element is refused");
    }
}
