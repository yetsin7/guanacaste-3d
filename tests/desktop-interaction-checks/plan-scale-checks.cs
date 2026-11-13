using System.IO;
using System.Windows;
using System.Windows.Controls;
using Ellipse = System.Windows.Shapes.Ellipse;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Verifies a plan scale changes annotation size, survives a project round trip, and remains undoable.</summary>
    private static void PlanScaleChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var view = session.Document.Views.First(item => item.Kind == ProjectViewKind.Plan && item.LevelId == level);
        var originalScale = view.DrawingScale;
        var originalZoom = Field<double>("_scale");
        var wall = session.AddWall(level, new Point2(900, 900), new Point2(904, 900));
        var axis = session.AddGridAxis("S" + Guid.NewGuid().ToString("N")[..8], new Point2(901, 899), new Point2(901, 905));
        var path = Path.Combine(Path.GetTempPath(), "plan-scale-" + Guid.NewGuid().ToString("N") + ".g3d");
        try
        {
            SetField("_scale", 40.0);
            var dimension = PlanDimensions.ForSelection(session.Document, wall)
                .Single(item => item.Kind == PlanDimensionKind.WallLength);
            var dimensionId = session.AddDimension(dimension);
            Invoke("RenderPlan");
            var label = _canvas.Children.OfType<Button>().Single(button => button.Tag is PlanDimension item &&
                item.Id == dimensionId && button.MinWidth == 0);
            var baseline = label.FontSize;
            var axisHead = _canvas.Children.OfType<Border>().First(item => item.Tag is Guid id && id == axis);
            var axisWidth = axisHead.Width;
            var marker = _canvas.Children.OfType<Ellipse>().First(item => item.Tag is Guid id && id == BuiltInProjectViews.NorthElevationId);
            var markerWidth = marker.Width;
            var picker = (ComboBox)_window.FindName("PlanScalePicker");
            picker.SelectedItem = picker.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, "200"));
            label = _canvas.Children.OfType<Button>().Single(button => button.Tag is PlanDimension item &&
                item.Id == dimensionId && button.MinWidth == 0);
            var expectedRatio = 200.0 / originalScale;
            Check(session.Document.Views.Single(item => item.Id == view.Id).DrawingScale == 200 &&
                Math.Abs(label.FontSize / baseline - expectedRatio) < 1e-6,
                "The plan scale control sizes permanent dimension labels in paper units");
            axisHead = _canvas.Children.OfType<Border>().First(item => item.Tag is Guid id && id == axis);
            marker = _canvas.Children.OfType<Ellipse>().First(item => item.Tag is Guid id && id == BuiltInProjectViews.NorthElevationId);
            Check(Math.Abs(axisHead.Width / axisWidth - expectedRatio) < 1e-6 &&
                  Math.Abs(marker.Width / markerWidth - expectedRatio) < 1e-6,
                "Plan scale resizes grid bubbles and elevation markers in paper units");
            SetField("_scale", 80.0);
            Invoke("RenderPlan");
            var zoomedHead = _canvas.Children.OfType<Border>().First(item => item.Tag is Guid id && id == axis);
            Check(Math.Abs(zoomedHead.Width / axisHead.Width - 2) < 1e-6,
                "Zoom resizes paper annotations proportionally with model geometry");
            ProjectFile.Save(path, session.Document);
            Check(ProjectFile.Load(path).Views.Single(item => item.Id == view.Id).DrawingScale == 200,
                "The selected plan scale survives saving and reopening the project");
            session.Undo();
            Check(session.Document.Views.Single(item => item.Id == view.Id).DrawingScale == originalScale,
                "Undo restores the previous plan drawing scale");
        }
        finally
        {
            SetField("_scale", originalZoom);
            if (File.Exists(path)) File.Delete(path);
            session.RemoveGridAxis(axis);
            if (session.Document.Walls.Any(item => item.Id == wall)) session.RemoveWall(wall);
        }
    }
}
