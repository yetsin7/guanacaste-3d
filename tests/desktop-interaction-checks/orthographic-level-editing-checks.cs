using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Edits a level datum in both orthographic views and verifies transactional undo.</summary>
    private static void OrthographicLevelEditingChecks(DocumentSession session)
    {
        var levelId = session.AddLevel();
        var original = session.Document.Levels.Single(level => level.Id == levelId).Elevation;
        var baseLevel = session.Document.Levels.OrderBy(level => level.Elevation).First();
        var wallId = session.AddWall(baseLevel.Id, new Point2(390, 390), new Point2(394, 390));
        session.SetWallTopConstraint(wallId, levelId);
        var originalHeight = session.Document.Walls.Single(wall => wall.Id == wallId).Height;
        try
        {
            foreach (var (viewField, canvasName) in new[]
            {
                ("_elevationViewId", "ElevationCanvas"), ("_sectionViewId", "SectionCanvas")
            })
            {
                Invoke("SelectProjectView", Field<Guid>(viewField));
                Pump();
                var canvas = (Canvas)_window.FindName(canvasName);
                var button = canvas.Children.OfType<StackPanel>().SelectMany(row => row.Children.OfType<Button>())
                    .Single(candidate => candidate.Tag is Guid id && id == levelId);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                var editor = canvas.Children.OfType<StackPanel>().SelectMany(row => row.Children.OfType<TextBox>())
                    .Single(candidate => candidate.Tag is Guid id && id == levelId);
                editor.Text = (original + 0.25).ToString(System.Globalization.CultureInfo.InvariantCulture) + " m";
                var previousSceneVersion = Field<int>("_sceneVersion");
                PressKey(editor, Key.Enter);
                Check(Math.Abs(session.Document.Levels.Single(level => level.Id == levelId).Elevation - original - 0.25) < 1e-8,
                    "Entering a level datum in an orthographic view moves the level");
                Check(Math.Abs(session.Document.Walls.Single(wall => wall.Id == wallId).Height - originalHeight - 0.25) < 1e-8 &&
                      Field<int>("_sceneVersion") > previousSceneVersion,
                    "The level edit regenerates constrained walls and invalidates the 3D scene");
                session.Undo();
                Check(Math.Abs(session.Document.Levels.Single(level => level.Id == levelId).Elevation - original) < 1e-8,
                    "Undo restores the level datum edited from an orthographic view");
            }
        }
        finally
        {
            Invoke("SelectProjectView", session.Document.Views.First(view => view.Kind == ProjectViewKind.Plan).Id);
            session.RemoveWall(wallId);
            session.RemoveLevel(levelId);
            Pump();
        }
    }
}
