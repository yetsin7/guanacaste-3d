using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Interop;
using Guanacaste3D.Desktop;

internal static partial class DocumentationChecks
{
    /// <summary>Checks real protected-level controls, family editing, independent scales, and preview regeneration.</summary>
    private static void CheckSheetDesktop(DocumentSession session)
    {
        var floor = session.Document.Levels.Single(level => level.Role == LevelRole.BaseFloor);
        var roof = session.Document.Levels.Single(level => level.Role == LevelRole.BaseRoof);
        var levels = new LevelsWindow(session) { Owner = _window };
        levels.Show(); Pump();
        var levelList = (ListBox)levels.FindName("LevelsList");
        levelList.SelectedValue = floor.Id; Pump();
        Check(!((Button)levels.FindName("DeleteLevelButton")).IsEnabled,
            "The level editor disables deletion for the base floor");
        levelList.SelectedValue = roof.Id; Pump();
        Check(!((Button)levels.FindName("DeleteLevelButton")).IsEnabled,
            "The level editor disables deletion for the base roof");
        levels.Close();
        var id = session.AddSheet(SheetPaperSize.A3, "Architectural drawing set");
        SetField("_selectedSheetId", (Guid?)id);
        ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 3;
        Invoke("RefreshAll"); Pump();
        var canvas = (Canvas)_window.FindName("SheetCanvas");
        Check(canvas.Width == 297 && canvas.Height == 420 && canvas.ContextMenu!.Items.Count >= 3,
            "The sheet workspace uses portrait physical dimensions and exposes contextual composition editing");
        Check(((TextBlock)_window.FindName("PropertiesText")).Text.Contains("297") &&
            ((TextBox)_window.FindName("PropertyLengthInput")).IsVisible == false,
            "Sheet properties show physical paper dimensions without unrelated wall editing fields");
        Invoke("SheetCanvas_MouseWheel", canvas, new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, 120)
            { RoutedEvent = System.Windows.Input.Mouse.MouseWheelEvent });
        Check(((System.Windows.Media.MatrixTransform)((Viewbox)canvas.Parent).RenderTransform).Matrix.M11 > 1,
            "The sheet workspace supports magnification without changing printed dimensions");
        ((Viewbox)canvas.Parent).RenderTransform = new System.Windows.Media.MatrixTransform();
        var editor = new SheetEditorWindow(session, id, () => Invoke("RefreshAll")) { Owner = _window };
        editor.Show(); Pump();
        var editorType = typeof(SheetEditorWindow);
        var choices = (ComboBox)editorType.GetField("_views", PrivateInstance)!.GetValue(editor)!;
        choices.SelectedValue = BuiltInProjectViews.NorthElevationId;
        editorType.GetMethod("AddView_Click", PrivateInstance)!.Invoke(editor, [editor, new RoutedEventArgs()]);
        var placements = (ObservableCollection<SheetViewportRow>)editorType.GetField("_placements", PrivateInstance)!.GetValue(editor)!;
        placements[0].Scale = 100; placements[1].Scale = 50;
        var graphics = (ObservableCollection<SheetGraphicRow>)editorType.GetField("_graphics", PrivateInstance)!.GetValue(editor)!;
        editorType.GetMethod("AddText_Click", PrivateInstance)!.Invoke(editor, [editor, new RoutedEventArgs()]);
        graphics.Last().Text = "Family editor verification"; graphics.Last().StartY = 36;
        editorType.GetMethod("Apply_Click", PrivateInstance)!.Invoke(editor, [editor, new RoutedEventArgs()]);
        Pump();
        var sheet = session.Document.Sheets.Single(sheet => sheet.Id == id);
        Check(sheet.Viewports.Count == 2 && sheet.Viewports.Select(viewport => viewport.Scale).SequenceEqual(new[] { 100d, 50d }),
            "Applying the sheet editor stores independently scaled plan and elevation placements");
        var family = session.Document.SheetFamilies.Single(family => family.Id == sheet.TitleBlockFamilyId);
        Check(family.SheetLayout!.Graphics.Any(graphic => graphic.Text == "Family editor verification"),
            "The family tab commits editable paper graphics to the native family definition");
        editor.Close(); Pump();
        Check(canvas.Children.OfType<Canvas>().Count() == 2 && canvas.Children.OfType<TextBlock>().Any(label => label.Text == "Family editor verification"),
            "The sheet preview regenerates both placed drawings and edited family fields");
        SaveBitmap("editable-sheet-workspace.png");
        var output = Path.GetFullPath("artifacts/documentation-evidence");
        File.WriteAllBytes(Path.Combine(output, "desktop-edited-sheets.pdf"),
            PdfPlanExporter.ExportDocumentation(session.Document, SheetPaperSize.A4, DateTimeOffset.UnixEpoch));
        var familyPath = Path.Combine(output, "desktop-sheet-family.g3df"); NativeDocumentStore.SaveFamily(familyPath, family);
        var before = session.Document.Sheets.Count;
        Check(Invoke("TryImportSheetFamily", familyPath) is true && session.Document.Sheets.Count == before + 1,
            "The associated .g3df route recognizes title-block families and opens a reusable sheet");
        var imported = session.Document.Sheets.Last();
        session.Undo();
        Check(session.Document.Sheets.All(sheet => sheet.Id != imported.Id),
            "Undo removes the imported sheet as one transaction without deleting its source file");
    }
}
