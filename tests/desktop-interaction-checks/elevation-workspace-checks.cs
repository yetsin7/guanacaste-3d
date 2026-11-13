using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Exercises elevation selection, clipping, and non-destructive close and reopen behavior.</summary>
    private static void ElevationWorkspaceChecks(DocumentSession session)
    {
        var level = Field<Guid>("_activeLevelId");
        var wall = session.AddWall(level, new Point2(800, 800), new Point2(804, 800));
        var viewId = session.Document.Views.First(view => view.Kind == ProjectViewKind.Elevation &&
            view.Orientation == ViewOrientation.North).Id;
        try
        {
            Invoke("SelectProjectView", viewId);
            Pump();
            var canvas = (Canvas)_window.FindName("ElevationCanvas");
            Check(canvas.Parent is FrameworkElement { ClipToBounds: true },
                "Elevation zoom is clipped to its viewport");
            var section = (Canvas)_window.FindName("SectionCanvas");
            Check(section.Parent is FrameworkElement { ClipToBounds: true },
                "Section zoom is clipped to its viewport");
            var shape = canvas.Children.OfType<Shape>().First(item => item.Tag is Guid id && id == wall);
            shape.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent
            });
            Pump();
            var selected = canvas.Children.OfType<Shape>().First(item => item.Tag is Guid id && id == wall);
            Check(Field<Guid?>("_selectedWallId") == wall && selected.Fill is SolidColorBrush { Color: { B: 230 } },
                "An elevation wall selects its complete profile and exposes its properties");
            var count = session.Document.Views.Count;
            Invoke("CloseElevationShortcut", viewId);
            Check(session.Document.Views.Count == count && session.Document.Views.Any(view => view.Id == viewId) &&
                Field<HashSet<Guid>>("_closedElevationShortcuts").Contains(viewId),
                "Closing an elevation shortcut preserves the project view");
            Invoke("SelectProjectView", viewId);
            Check(!Field<HashSet<Guid>>("_closedElevationShortcuts").Contains(viewId),
                "Opening a project view restores its closed shortcut");
            var previousSection = Field<Guid>("_sectionViewId");
            var sectionId = session.AddSectionView("Selection test", new SectionCut(new Point2(802, 798), new Point2(802, 802)));
            try
            {
                Invoke("SelectProjectView", sectionId);
                Pump();
                var sectionProfile = section.Children.OfType<Shape>().First(item => item.Tag is Guid id && id == wall &&
                    (item is Polyline { Points.Count: > 1 } || item is System.Windows.Shapes.Path { Data: { } geometry } && !geometry.IsEmpty()));
                sectionProfile.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent
                });
                Pump();
                Check(Field<Guid?>("_selectedWallId") == wall && section.Children.OfType<Shape>()
                        .Any(item => item.Tag is Guid id && id == wall && item.Fill is SolidColorBrush { Color: { B: 230 } }),
                    "A section profile selects and highlights its original wall");
            }
            finally
            {
                session.Undo();
                SetField("_sectionViewId", previousSection);
            }
        }
        finally
        {
            SetField("_selectedWallId", null);
            var tabs = (TabControl)_window.FindName("ViewTabs");
            tabs.SelectedIndex = 0;
            session.RemoveWall(wall);
        }
    }
}
