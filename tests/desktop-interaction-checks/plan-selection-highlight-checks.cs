using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Checks that plan selection colors the element itself and retains its selectable identity.</summary>
    private static void PlanSelectionHighlightChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var wall = session.AddWall(level, new Point2(730, 730), new Point2(734, 730));
        try
        {
            SetField("_selectedWallId", (Guid?)wall);
            SetField("_selectedOpeningId", null);
            SetField("_viewSelection", null);
            Invoke("RefreshProperties");
            Invoke("RenderPlan");
            var wallShape = _canvas.Children.OfType<Polygon>().First(shape => Equals(shape.Tag, wall));
            Check(wallShape.Fill is SolidColorBrush { Color: { B: > 150, A: > 0 } } &&
                wallShape.Stroke is SolidColorBrush { Color: { B: > 150 } } && wallShape.IsHitTestVisible,
                "Selected wall is filled blue and remains selectable in plan");
            Check(!string.IsNullOrWhiteSpace(((TextBlock)_window.FindName("PropertiesText")).Text),
                "The properties panel remains populated for the selected wall");
        }
        finally
        {
            SetField("_selectedWallId", null);
            session.RemoveWall(wall);
        }
    }
}
