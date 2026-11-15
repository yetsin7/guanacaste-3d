using System.Globalization;
using Guanacaste3D.Core.Interop;

namespace Guanacaste3D.Core;

/// <summary>
/// Tables that complete the engineering sheets, kept as generated views so they are placed on a sheet like any view and
/// always show the current model: the electrical load schedule, the plumbing schedule, the symbol legend and the
/// construction budget. They are drawn straight in paper millimetres, as the area schedule is.
/// </summary>
public static class EngineeringTables
{
    public static readonly Guid PanelScheduleId = Guid.Parse("4f1d6c2a-8b3e-4a71-9c05-2e6b7d1f3a90");
    public static readonly Guid PlumbingScheduleId = Guid.Parse("a7c3e915-2d4b-4f68-b1e0-6c8d2f5a7b14");
    public static readonly Guid SymbolLegendId = Guid.Parse("c2e8f4a1-7b36-4d59-8e12-9a4f6b3c1d07");
    public static readonly Guid BudgetTableId = Guid.Parse("e9b5a3d7-1c48-4e2f-a6b0-3d7f8c2e5a61");

    private static readonly (Guid Id, string Label)[] Tables =
    [
        (PanelScheduleId, "PanelSchedule.Title"), (PlumbingScheduleId, "PlumbingSchedule.Title"),
        (SymbolLegendId, "SymbolLegend.Title"), (BudgetTableId, "BudgetTable.Title")
    ];

    private static readonly (double, double, double) Ink = (0, 0, 0);

    public static bool IsEngineeringTable(Guid viewId) => Tables.Any(table => table.Id == viewId);

    /// <summary>Adds the tables to projects made before they existed; a name already used by the user gets a suffix.</summary>
    public static void Ensure(List<ProjectView> views)
    {
        foreach (var (id, label) in Tables)
        {
            if (views.Any(view => view.Id == id)) continue;
            var name = DefaultLabels.Get(label);
            var unique = name;
            for (var number = 2; views.Any(view => string.Equals(view.Name, unique, StringComparison.CurrentCultureIgnoreCase)); number++)
                unique = $"{name} {number}";
            views.Add(new ProjectView(id, unique, ProjectViewKind.Schedule, null, ViewOrientation.None));
        }
    }

    /// <summary>The table of a generated view, laid out inside its placement on the sheet.</summary>
    public static IReadOnlyList<SheetPrimitive> Primitives(BuildingDocument document, Guid viewId, SheetViewport placement)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (viewId == PanelScheduleId) return PanelSchedule(document, placement);
        if (viewId == PlumbingScheduleId) return PlumbingSchedule(document, placement);
        if (viewId == SymbolLegendId) return SymbolLegend(placement);
        if (viewId == BudgetTableId) return BudgetTable(document, placement);
        return [];
    }

    private static string N(double value, string format = "0") => value.ToString(format, CultureInfo.CurrentCulture);

    private static IReadOnlyList<SheetPrimitive> PanelSchedule(BuildingDocument document, SheetViewport placement)
    {
        var rows = new List<(string[] Cells, bool Bold)>();
        foreach (var panel in ElectricalAnalysis.Analyze(document).Panels)
        {
            rows.Add(([panel.Panel.Name, "", "", "", "", "", ""], true));
            foreach (var circuit in panel.Circuits.OrderBy(circuit => circuit.Circuit.Number))
                rows.Add(([$"C{circuit.Circuit.Number}", circuit.Circuit.Name, $"{N(circuit.Circuit.BreakerAmps)} A · {N(circuit.Circuit.Voltage)} V",
                    $"{N(circuit.LoadWatts)} W", $"{circuit.ConductorAwg} AWG ({N(circuit.ConductorSquareMillimeters, "0.#")} mm²)",
                    $"{N(circuit.VoltageDropPercent, "0.0")} %", Status(circuit.Status)], false));
            rows.Add((["", "Total del tablero", "", $"{N(panel.LoadWatts)} W", $"{N(panel.Current, "0.0")} A", $"{N(panel.Utilization * 100)} % de uso", Status(panel.Status)], true));
        }
        if (rows.Count == 0) rows.Add((["", "No hay tableros en el proyecto.", "", "", "", "", ""], false));
        return Table(placement, ["Circuito", "Descripción", "Protección", "Carga", "Conductor", "Caída", "Estado"],
            [0.1, 0.24, 0.16, 0.1, 0.2, 0.08, 0.12], rows);
    }

    private static string Status(CircuitStatus status) => status switch
    {
        CircuitStatus.Ok => "Cumple",
        CircuitStatus.Empty => "Sin aparatos",
        CircuitStatus.Warning => "Revisar",
        _ => "No cumple"
    };

    private static IReadOnlyList<SheetPrimitive> PlumbingSchedule(BuildingDocument document, SheetViewport placement)
    {
        var rows = new List<(string[] Cells, bool Bold)>();
        if (document.PlumbingSystems.Count > 0)
            foreach (var system in PlumbingAnalysis.Analyze(document).Systems)
                rows.Add(([system.System.Name, SystemKind(system.System.Kind), system.SourceName, N(system.Fixtures.Count),
                    N(system.Units, "0.#"), $"Ø{system.MainNominal}", $"{N(system.LengthMeters, "0.0")} m", system.Status == PlumbingStatus.Ok ? "Cumple" : "Revisar"], false));
        if (rows.Count == 0) rows.Add((["No hay sistemas: conecte la plomería.", "", "", "", "", "", "", ""], false));
        return Table(placement, ["Sistema", "Tipo", "Origen", "Aparatos", "Unidades", "Tubería principal", "Longitud", "Estado"],
            [0.18, 0.14, 0.14, 0.09, 0.09, 0.14, 0.1, 0.12], rows);
    }

    private static string SystemKind(PlumbingSystemKind kind) => kind switch
    {
        PlumbingSystemKind.ColdWater => "Agua fría",
        PlumbingSystemKind.HotWater => "Agua caliente",
        PlumbingSystemKind.StormWater => "Pluvial",
        _ => "Aguas negras"
    };

    /// <summary>The symbols the plans print, drawn with the same code that draws them on the plan sheets.</summary>
    private static IReadOnlyList<SheetPrimitive> SymbolLegend(SheetViewport placement)
    {
        var entries = new (ElectricalDeviceKind? Device, string Text)[]
        {
            (ElectricalDeviceKind.Outlet, "Tomacorriente doble 120 V"), (ElectricalDeviceKind.Switch, "Apagador sencillo"),
            (ElectricalDeviceKind.CeilingFixture, "Luminaria de techo"), (ElectricalDeviceKind.Panel, "Tablero eléctrico"),
            (null, "Retorno al tablero (circuito)")
        };
        var graphics = new List<SheetPrimitive>();
        var rowHeight = Math.Clamp(placement.Height / (entries.Length + 6), 6, 10);
        var y = placement.Y + rowHeight;
        graphics.Add(new SheetText(new(placement.X + 2, placement.Y + 1), "Simbología", rowHeight * 0.6, Ink, true));
        foreach (var (device, text) in entries)
        {
            var centre = new Point2(placement.X + 8, y + rowHeight / 2);
            if (device is { } kind)
            {
                // Symbols are drawn in metres on plans; 1 m becomes 10 mm here so they read at legend size.
                var symbol = PdfPlanExporter.ElectricalSymbolPrimitives(kind, new Point2(0, 0));
                graphics.AddRange(symbol.Select(primitive => Scale(primitive, centre, 10)));
            }
            else graphics.Add(new SheetPolyline([new(centre.X - 5, centre.Y), new(centre.X + 5, centre.Y)], 0.25, (0.75, 0.1, 0.1), true));
            graphics.Add(new SheetText(new(placement.X + 18, y + rowHeight * 0.2), text, rowHeight * 0.5, Ink));
            y += rowHeight;
        }
        foreach (var (kind, text) in new[] { (PlumbingSystemKind.ColdWater, "Agua fría"), (PlumbingSystemKind.HotWater, "Agua caliente"),
                     (PlumbingSystemKind.Sanitary, "Aguas negras (con pendiente)"), (PlumbingSystemKind.StormWater, "Aguas pluviales") })
        {
            var centre = new Point2(placement.X + 8, y + rowHeight / 2);
            graphics.Add(new SheetPolyline([new(centre.X - 5, centre.Y), new(centre.X + 5, centre.Y)], kind == PlumbingSystemKind.Sanitary ? 0.5 : 0.35,
                PdfPlanExporter.PipeColor(kind), kind != PlumbingSystemKind.ColdWater));
            graphics.Add(new SheetText(new(placement.X + 18, y + rowHeight * 0.2), text, rowHeight * 0.5, Ink));
            y += rowHeight;
        }
        return graphics;
    }

    private static SheetPrimitive Scale(SheetPrimitive primitive, Point2 centre, double factor)
    {
        // Plan symbols are drawn with Y up; the sheet's paper Y runs down.
        Point2 Map(Point2 point) => new(centre.X + point.X * factor, centre.Y - point.Y * factor);
        return primitive switch
        {
            SheetPolygon polygon => polygon with { Points = polygon.Points.Select(Map).ToList() },
            SheetPolyline line => line with { Points = line.Points.Select(Map).ToList() },
            SheetText text => text with { Position = Map(text.Position), Size = Math.Min(text.Size, 2.5) },
            _ => primitive
        };
    }

    private static IReadOnlyList<SheetPrimitive> BudgetTable(BuildingDocument document, SheetViewport placement)
    {
        var settings = BudgetSettings.FromJson(document.Extensions.GetValueOrDefault(BudgetSettings.Key));
        var rows = new List<(string[] Cells, bool Bold)>();
        string Money(decimal value) => (settings.Currency == "CRC" ? "₡" : settings.Currency + " ") + value.ToString("#,0", CultureInfo.CurrentCulture);
        try
        {
            var budget = ConstructionBudget.Build(document, settings);
            foreach (var (category, total) in budget.ByCategory()) rows.Add(([Trade(category), Money(total)], false));
            rows.Add((["Materiales", Money(budget.Materials)], true));
            rows.Add(([$"Mano de obra ({N((double)settings.LaborRate * 100)} %)", Money(budget.Labor)], false));
            rows.Add(([$"Imprevistos ({N((double)settings.ContingencyRate * 100)} %)", Money(budget.Contingency)], false));
            rows.Add((["TOTAL", Money(budget.Total)], true));
        }
        catch (InvalidDataException) { rows.Add((["El modelo no se puede cuantificar todavía.", ""], false)); }
        return Table(placement, ["Rubro", "Monto"], [0.65, 0.35], rows);
    }

    private static string Trade(MaterialCategory category) => category switch
    {
        MaterialCategory.Foundations => "Cimentación",
        MaterialCategory.Structure => "Estructura y concreto",
        MaterialCategory.Masonry => "Mampostería",
        MaterialCategory.Drywall => "Tabla yeso y divisiones",
        MaterialCategory.Plaster => "Repellos y finos",
        MaterialCategory.Roofing => "Techos",
        MaterialCategory.Ceilings => "Cielos",
        MaterialCategory.Floors => "Pisos",
        MaterialCategory.Paint => "Pintura",
        MaterialCategory.Furnishings => "Mobiliario y equipo",
        _ => "Puertas y ventanas"
    };

    /// <summary>A ruled table filling the placement's width, with a header row and one row per entry.</summary>
    private static IReadOnlyList<SheetPrimitive> Table(SheetViewport placement, string[] headers, double[] widths,
        IReadOnlyList<(string[] Cells, bool Bold)> rows)
    {
        var graphics = new List<SheetPrimitive>();
        var rowHeight = Math.Clamp(placement.Height / (rows.Count + 2), 3.5, 7);
        var size = rowHeight * 0.5;
        var left = placement.X;
        var right = placement.X + placement.Width;
        var stops = new List<double> { left };
        foreach (var width in widths) stops.Add(stops[^1] + width * placement.Width);
        var y = placement.Y;
        void Rule(double at, double weight) => graphics.Add(new SheetPolyline([new(left, at), new(right, at)], weight, Ink));
        void Row(string[] cells, bool bold)
        {
            for (var index = 0; index < cells.Length && index < widths.Length; index++)
                if (cells[index].Length > 0)
                    graphics.Add(new SheetText(new(stops[index] + 1, y + (rowHeight - size) / 2), cells[index], size, Ink, bold));
        }
        Rule(y, 0.35);
        Row(headers, true);
        y += rowHeight;
        Rule(y, 0.35);
        foreach (var (cells, bold) in rows)
        {
            if (y + rowHeight > placement.Y + placement.Height) break;
            Row(cells, bold);
            y += rowHeight;
            Rule(y, bold ? 0.25 : 0.1);
        }
        foreach (var x in stops) graphics.Add(new SheetPolyline([new(x, placement.Y), new(x, y)], 0.15, Ink));
        return graphics;
    }
}
