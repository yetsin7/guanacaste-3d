using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Checks fitting of a large, displaced plan while ignoring hidden geometry and remote view symbols.</summary>
    private static void PlanFitChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var visible = session.AddWall(level, new Point2(10000, 10000), new Point2(10300, 10000));
        var hidden = session.AddWall(level, new Point2(-20000, -20000), new Point2(-19996, -20000));
        var plan = session.Document.Views.First(view => view.Kind == ProjectViewKind.Plan && view.LevelId == level);
        var distantFloor = session.AddFloor(level, [new Point2(30000, 30000), new Point2(30004, 30000),
            new Point2(30004, 30004), new Point2(30000, 30004)], 0.2, elevationOffset: 5);
        var distantColumn = session.AddColumn(level, new Point2(40000, 40000));
        var column = session.Document.Columns.Single(item => item.Id == distantColumn);
        session.UpdateColumn(distantColumn, column.Width, column.Depth, 1, column.Shape);
        var elevationId = BuiltInProjectViews.NorthElevationId;
        var originalMarker = session.Document.Views.Single(view => view.Id == elevationId).ElevationMarker;
        try
        {
            session.SetViewVisibility(plan.Id, [hidden], [visible, distantFloor, distantColumn]);
            session.UpdatePlanViewRange(plan.Id, new PlanViewRange(1.5, 1.6, 2));
            session.UpdateElevationMarker(elevationId, new ElevationMarker(new Point2(-10000, 10000), 20, 20));
            Invoke("FitPlan_Click", _window, new RoutedEventArgs());
            var scale = Field<double>("_scale");
            var start = (Point)Invoke("WorldToCanvas", new Point2(10000, 10000))!;
            var end = (Point)Invoke("WorldToCanvas", new Point2(10300, 10000))!;
            Check(scale < 8 && start.X >= 0 && end.X <= _canvas.ActualWidth &&
                  Math.Abs((start.X + end.X) / 2 - _canvas.ActualWidth / 2) < 0.1 &&
                  Math.Abs(start.Y - _canvas.ActualHeight / 2) < 0.1,
                "Fit plan centers all visible model geometry even on a large site with distant hidden items and markers");
        }
        finally
        {
            session.SetViewVisibility(plan.Id, plan.HiddenElementIds, plan.IsolatedElementIds);
            session.UpdatePlanViewRange(plan.Id, plan.Range ?? PlanViewRange.Architectural);
            var viewIndex = session.Document.Views.FindIndex(view => view.Id == elevationId);
            session.Document.Views[viewIndex] = session.Document.Views[viewIndex] with { ElevationMarker = originalMarker };
            session.RemoveFloor(distantFloor);
            session.RemoveColumn(distantColumn);
            session.RemoveWall(visible);
            session.RemoveWall(hidden);
        }
    }
}
