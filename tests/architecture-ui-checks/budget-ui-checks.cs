using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;

/// <summary>
/// Opens the material takeoff and budget through the same command the ribbon sends, on the two-storey sample house,
/// and checks that it lists priced materials, follows the open project, saves a price change, and is a single window.
/// </summary>
internal static class BudgetUiChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(MainWindow main, Action<bool, string> check, Action pump)
    {
        var house = new DocumentSession(TwoStoreyHouse.Create());
        typeof(MainWindow).GetField("_session", Private)!.SetValue(main, house);
        var executed = (bool)typeof(MainWindow).GetMethod("ExecuteApplicationCommand", Private)!.Invoke(main, ["MaterialBudget"])!;
        pump();
        var windows = BudgetWindows();
        check(executed && windows.Length == 1, "Budget: the Analyze ribbon command opens the takeoff and budget window");
        var budget = windows[0];
        budget.Width = 1180;
        budget.Height = 720;
        pump();

        var grid = (DataGrid)Field(budget, "_grid");
        var totals = ((TextBlock)Field(budget, "_totals")).Text;
        var rows = ((IEnumerable)grid.ItemsSource).Cast<object>().ToArray();
        check(rows.Length >= 10 && totals.Contains("TOTAL", StringComparison.Ordinal) && totals.Contains('₡'),
            "Budget: the sample house lists at least ten priced materials and a total in colones");
        check(grid.Columns.Count(column => !column.IsReadOnly) == 1, "Budget: only the unit price column is editable");
        SaveBitmap(budget, "artifacts/interaction-evidence/material-budget.png");

        typeof(MainWindow).GetMethod("ExecuteApplicationCommand", Private)!.Invoke(main, ["MaterialTakeoff"]);
        pump();
        check(BudgetWindows().Length == 1, "Budget: the View ribbon takeoff command reuses the open window");

        var settings = house.GetBudgetSettings().WithPrice(MaterialCatalog.Cement, 9_999m);
        budget.GetType().GetMethod("Save", Private)!.Invoke(budget, [settings]);
        check(house.GetBudgetSettings().PriceOf(MaterialCatalog.Cement) == 9_999m && house.CanUndo,
            "Budget: a price edited in the window is saved in the project as an undoable step");

        // Opening another project replaces the main window's session; the open budget must follow it.
        typeof(MainWindow).GetField("_session", Private)!.SetValue(main, new DocumentSession(BuildingDocument.Create("Empty")));
        budget.GetType().GetMethod("Refresh", Private)!.Invoke(budget, null);
        check(((IEnumerable)grid.ItemsSource).Cast<object>().Count() == 0,
            "Budget: the open window follows the project that is open now");
        budget.Close();
        pump();
    }

    private static Window[] BudgetWindows() =>
        Application.Current.Windows.Cast<Window>().Where(window => window.GetType().Name == "MaterialBudgetWindow").ToArray();

    private static object Field(object owner, string name) => owner.GetType().GetField(name, Private)!.GetValue(owner)!;

    private static void SaveBitmap(Window window, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            context.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
