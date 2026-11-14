using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop;

/// <summary>
/// Material takeoff and construction budget. Every line is computed from the model when the window opens or gets
/// focus again, so drawing a wall and coming back shows the new blocks and cement at once. Unit prices, labor and
/// contingency are edited here and saved in the project as undoable steps.
/// </summary>
internal sealed class MaterialBudgetWindow : Window
{
    // The main window replaces its session when another project is opened; asking each time follows it.
    private readonly Func<DocumentSession> _currentSession;
    private DocumentSession _session => _currentSession();
    private readonly Action _changed;
    private readonly DataGrid _grid = new()
    {
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        RowHeaderWidth = 0,
        SelectionUnit = DataGridSelectionUnit.CellOrRowHeader
    };
    private readonly TextBox _labor = new() { Width = 64, Margin = new Thickness(4, 0, 16, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _contingency = new() { Width = 64, Margin = new Thickness(4, 0, 16, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _totals = new() { Margin = new Thickness(0, 8, 0, 0), FontSize = 14, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _trades = new() { Margin = new Thickness(0, 4, 0, 0), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notes = new() { Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap };
    private ConstructionBudget? _budget;

    /// <summary>One row of the grid; only the unit price is editable.</summary>
    public sealed class Row
    {
        public required string Code { get; init; }
        public required string Trade { get; init; }
        public required string Material { get; init; }
        public required string Unit { get; init; }
        public required string Net { get; init; }
        public required string Purchase { get; init; }
        public required string Price { get; set; }
        public required string Total { get; init; }
        public required string Sources { get; init; }
    }

    public MaterialBudgetWindow(Func<DocumentSession> session, Action changed)
    {
        _currentSession = session;
        _changed = changed;
        Title = Text("Budget.Title");
        Width = 1180;
        Height = 720;
        MinWidth = 760;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildColumns();
        _grid.CellEditEnding += Grid_CellEditEnding;

        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        toolbar.Children.Add(Label("Budget.Labor"));
        toolbar.Children.Add(_labor);
        toolbar.Children.Add(Label("Budget.Contingency"));
        toolbar.Children.Add(_contingency);
        toolbar.Children.Add(Button("Budget.Apply", (_, _) => ApplyRates()));
        toolbar.Children.Add(Button("Budget.ExportTakeoff", (_, _) => Export(takeoff: true)));
        toolbar.Children.Add(Button("Budget.ExportBudget", (_, _) => Export(takeoff: false)));
        toolbar.Children.Add(Button("Budget.ResetPrices", (_, _) => ResetPrices()));

        var hint = new TextBlock { Text = Text("Budget.Hint"), Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
        var footer = new StackPanel();
        footer.Children.Add(_totals);
        footer.Children.Add(_trades);
        footer.Children.Add(_notes);

        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(hint, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(toolbar);
        layout.Children.Add(hint);
        layout.Children.Add(footer);
        layout.Children.Add(_grid);
        Content = layout;

        Activated += (_, _) => Refresh();
        Refresh();
    }

    private void BuildColumns()
    {
        DataGridTextColumn Column(string header, string path, double width, bool readOnly = true, bool right = false)
        {
            var column = new DataGridTextColumn
            {
                Header = Text(header),
                Binding = new Binding(path) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay },
                Width = new DataGridLength(width, width > 5 ? DataGridLengthUnitType.Pixel : DataGridLengthUnitType.Star),
                IsReadOnly = readOnly
            };
            if (right)
                column.ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } };
            return column;
        }

        _grid.Columns.Add(Column("Budget.Column.Trade", nameof(Row.Trade), 130));
        _grid.Columns.Add(Column("Budget.Column.Material", nameof(Row.Material), 2));
        _grid.Columns.Add(Column("Budget.Column.Unit", nameof(Row.Unit), 100));
        _grid.Columns.Add(Column("Budget.Column.Net", nameof(Row.Net), 90, right: true));
        _grid.Columns.Add(Column("Budget.Column.Purchase", nameof(Row.Purchase), 90, right: true));
        var price = Column("Budget.Column.Price", nameof(Row.Price), 110, readOnly: false, right: true);
        price.CellStyle = new Style(typeof(DataGridCell)) { Setters = { new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1))) } };
        _grid.Columns.Add(price);
        _grid.Columns.Add(Column("Budget.Column.Total", nameof(Row.Total), 130, right: true));
        _grid.Columns.Add(Column("Budget.Column.Sources", nameof(Row.Sources), 1));
    }

    /// <summary>Recomputes the takeoff from the current model and redraws the grid and the totals.</summary>
    private void Refresh()
    {
        try
        {
            var settings = _session.GetBudgetSettings();
            _budget = ConstructionBudget.Build(_session.Document, settings);
            _labor.Text = (settings.LaborRate * 100).ToString("0.##", CultureInfo.CurrentCulture);
            _contingency.Text = (settings.ContingencyRate * 100).ToString("0.##", CultureInfo.CurrentCulture);
            _grid.ItemsSource = _budget.Lines.Select(line => new Row
            {
                Code = line.Takeoff.Code,
                Trade = Trade(line.Takeoff.Material.Category),
                Material = line.Takeoff.Material.Description,
                Unit = line.Takeoff.Material.Unit,
                Net = line.Takeoff.NetQuantity.ToString("#,0.##", CultureInfo.CurrentCulture),
                Purchase = line.Takeoff.PurchaseQuantity.ToString("#,0.##", CultureInfo.CurrentCulture),
                Price = line.UnitPrice.ToString("#,0.##", CultureInfo.CurrentCulture),
                Total = Money(line.Total),
                Sources = Summarize(line.Takeoff.Sources)
            }).ToList();

            _totals.Text = _budget.Lines.Count == 0
                ? Text("Budget.Empty")
                : string.Format(CultureInfo.CurrentCulture, Text("Budget.Totals"), Money(_budget.Materials), Money(_budget.Labor),
                    Money(_budget.Contingency), Money(_budget.Total));
            _trades.Text = string.Join("   ·   ", _budget.ByCategory().Select(trade => $"{Trade(trade.Category)}: {Money(trade.Total)}"));
            _notes.Text = string.Join(Environment.NewLine,
                _budget.Takeoff.Assumptions.Select(note => "⚠ " + note).Prepend(Text("Budget.ReferencePrices")));
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or KeyNotFoundException)
        {
            _grid.ItemsSource = null;
            _totals.Text = Text("Budget.ModelError") + " " + LocalizedErrors.Translate(exception.Message);
        }
    }

    private void Grid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not Row row || e.EditingElement is not TextBox box) return;
        if (!decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var price) || price < 0)
        {
            e.Cancel = true;
            MessageBox.Show(this, Text("Budget.InvalidPrice"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Rebuilding the rows inside the edit event would fight the grid's own commit, so it runs right after.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => Save(_session.GetBudgetSettings().WithPrice(row.Code, price)));
    }

    private void ApplyRates()
    {
        if (!TryPercent(_labor.Text, 500, out var labor) || !TryPercent(_contingency.Text, 100, out var contingency))
        {
            MessageBox.Show(this, Text("Budget.InvalidRate"), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Save(_session.GetBudgetSettings() with { LaborRate = labor, ContingencyRate = contingency });
    }

    private void ResetPrices()
    {
        var current = _session.GetBudgetSettings();
        Save(BudgetSettings.Default with { LaborRate = current.LaborRate, ContingencyRate = current.ContingencyRate });
    }

    private void Save(BudgetSettings settings)
    {
        try
        {
            _session.SetBudgetSettings(settings);
            _changed();
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(exception.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Refresh();
    }

    private void Export(bool takeoff)
    {
        if (_budget is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = Text("Budget.CsvFilter"),
            FileName = Text(takeoff ? "Budget.TakeoffFileName" : "Budget.BudgetFileName"),
            InitialDirectory = FileDialogLocations.ProjectFolder()
        };
        if (dialog.ShowDialog(this) != true) return;
        // UTF-8 with a byte order mark so a spreadsheet opens "m²" and the accents correctly.
        File.WriteAllText(dialog.FileName, takeoff ? _budget.Takeoff.ToCsv() : _budget.ToCsv(), new System.Text.UTF8Encoding(true));
    }

    private static bool TryPercent(string text, decimal maximum, out decimal rate)
    {
        rate = 0;
        if (!decimal.TryParse(text.Replace("%", string.Empty).Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var percent)
            || percent < 0 || percent > maximum) return false;
        rate = percent / 100;
        return true;
    }

    private string Money(decimal value)
    {
        var currency = _budget?.Settings.Currency ?? BudgetSettings.Default.Currency;
        var symbol = currency == "CRC" ? "₡" : currency + " ";
        return symbol + value.ToString("#,0", CultureInfo.CurrentCulture);
    }

    private static string Summarize(IReadOnlyList<string> sources) =>
        sources.Count <= 3 ? string.Join(", ", sources) : string.Join(", ", sources.Take(3)) + $" (+{sources.Count - 3})";

    private static string Trade(MaterialCategory category) => Text("Budget.Category." + category);

    private static TextBlock Label(string key) => new() { Text = Text(key), VerticalAlignment = VerticalAlignment.Center };

    private static Button Button(string key, RoutedEventHandler click)
    {
        var button = new Button { Content = Text(key), Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 3, 10, 3) };
        button.Click += click;
        return button;
    }

    private static string Text(string key) => Application.Current.TryFindResource(key) as string ?? key;
}
