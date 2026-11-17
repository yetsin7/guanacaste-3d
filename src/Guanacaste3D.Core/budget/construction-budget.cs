using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Guanacaste3D.Core;

/// <summary>
/// The project's budget settings: currency, the unit price of each catalog material and the rates applied on top
/// of the materials. They are saved inside the project (see <see cref="Key"/>), so every copy of the file prices
/// the same way. The default prices are Costa Rican reference values in colones and must be checked locally.
/// </summary>
public sealed record BudgetSettings(string Currency, IReadOnlyDictionary<string, decimal> Prices,
    decimal LaborRate, decimal ContingencyRate)
{
    public const string Key = "budget.settings";

    public static readonly IReadOnlyDictionary<string, decimal> CostaRicaReferencePrices = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        [MaterialCatalog.Block12] = 520m,
        [MaterialCatalog.Block15] = 640m,
        [MaterialCatalog.Block20] = 880m,
        [MaterialCatalog.Brick] = 360m,
        [MaterialCatalog.Cement] = 7_200m,
        [MaterialCatalog.Sand] = 22_000m,
        [MaterialCatalog.Stone] = 24_000m,
        [MaterialCatalog.BaseGravel] = 12_000m,
        [MaterialCatalog.Rebar2] = 1_300m,
        [MaterialCatalog.Rebar3] = 2_450m,
        [MaterialCatalog.Rebar4] = 4_300m,
        [MaterialCatalog.TieWire] = 1_600m,
        [MaterialCatalog.Mesh] = 28_000m,
        [MaterialCatalog.FinePlaster] = 4_500m,
        [MaterialCatalog.GypsumBoard] = 8_500m,
        [MaterialCatalog.Stud] = 3_200m,
        [MaterialCatalog.Track] = 2_800m,
        [MaterialCatalog.DrywallScrew] = 8m,
        [MaterialCatalog.FramingScrew] = 10m,
        [MaterialCatalog.JointCompound] = 14_000m,
        [MaterialCatalog.JointTape] = 2_500m,
        [MaterialCatalog.GlassPartition] = 85_000m,
        [MaterialCatalog.Paint] = 18_000m,
        [MaterialCatalog.Ceramic] = 9_500m,
        [MaterialCatalog.TileAdhesive] = 6_500m,
        [MaterialCatalog.Grout] = 2_200m,
        [MaterialCatalog.RoofSheet] = 9_800m,
        [MaterialCatalog.Purlin] = 14_500m,
        [MaterialCatalog.RoofScrew] = 90m,
        [MaterialCatalog.CeilingBoard] = 6_800m,
        [MaterialCatalog.CeilingProfile] = 2_600m,
        [MaterialCatalog.Door] = 95_000m,
        [MaterialCatalog.Hinge] = 2_500m,
        [MaterialCatalog.Lockset] = 18_000m,
        [MaterialCatalog.Window] = 65_000m,
        [MaterialCatalog.Stair] = 95_000m,
        [MaterialCatalog.Ramp] = 70_000m,
        [MaterialCatalog.Guard] = 48_000m,
        [MaterialCatalog.CurtainWall] = 120_000m,
        [MaterialCatalog.Skylight] = 85_000m,
        [MaterialCatalog.Gutter] = 6_500m,
        [MaterialCatalog.Downspout] = 5_200m,
        [MaterialCatalog.Outlet] = 4_800m,
        [MaterialCatalog.Switch] = 4_200m,
        [MaterialCatalog.Luminaire] = 14_000m,
        [MaterialCatalog.Panel] = 120_000m,
        [MaterialCatalog.MiniSplit] = 420_000m,
        [MaterialCatalog.Equipment] = 250_000m,
        [MaterialCatalog.LifeSafety] = 22_000m,
        [MaterialCatalog.Conductor] = 1_900m,
        [MaterialCatalog.Toilet] = 65_000m,
        [MaterialCatalog.Washbasin] = 45_000m,
        [MaterialCatalog.Bathtub] = 160_000m,
        [MaterialCatalog.Shower] = 55_000m,
        [MaterialCatalog.NetworkPoint] = 85_000m,
        [MaterialCatalog.Pipe] = 2_400m,
        [MaterialCatalog.Duct] = 18_000m,
    };

    public static BudgetSettings Default => new("CRC", CostaRicaReferencePrices, 0.40m, 0.10m);

    public decimal PriceOf(string code) => Prices.TryGetValue(code, out var price) ? price : 0m;

    /// <summary>The project's price for a line, or the line's reference price when the project sets none.</summary>
    public decimal PriceOf(MaterialDefinition material) =>
        Prices.TryGetValue(material.Code, out var price) ? price : material.ReferencePrice ?? 0m;

    /// <summary>Returns a copy with one unit price replaced.</summary>
    public BudgetSettings WithPrice(string code, decimal price)
    {
        if (!MaterialCatalog.IsPriceable(code)) MaterialCatalog.Get(code);
        if (price < 0) throw new InvalidDataException("A unit price cannot be negative.");
        var copy = new Dictionary<string, decimal>(Prices, StringComparer.Ordinal) { [code] = price };
        return this with { Prices = copy };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Currency) || Currency.Length > 8) throw new InvalidDataException("The budget currency is invalid.");
        if (LaborRate is < 0 or > 5) throw new InvalidDataException("The labor rate must be between 0 % and 500 %.");
        if (ContingencyRate is < 0 or > 1) throw new InvalidDataException("The contingency rate must be between 0 % and 100 %.");
        if (Prices.Values.Any(price => price < 0)) throw new InvalidDataException("A unit price cannot be negative.");
    }

    private sealed record Stored(string Currency, Dictionary<string, decimal> Prices, decimal LaborRate, decimal ContingencyRate);

    public string ToJson() => JsonSerializer.Serialize(new Stored(Currency, new(Prices, StringComparer.Ordinal), LaborRate, ContingencyRate));

    /// <summary>Reads saved settings; unknown codes are dropped and catalog materials missing from the file take the reference price.</summary>
    public static BudgetSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default;
        var stored = JsonSerializer.Deserialize<Stored>(json) ?? throw new InvalidDataException("The budget settings are empty.");
        var prices = new Dictionary<string, decimal>(CostaRicaReferencePrices, StringComparer.Ordinal);
        foreach (var (code, price) in stored.Prices ?? [])
            if (prices.ContainsKey(code) || MaterialCatalog.IsFurniture(code)) prices[code] = price;
        var settings = new BudgetSettings(stored.Currency, prices, stored.LaborRate, stored.ContingencyRate);
        settings.Validate();
        return settings;
    }
}

/// <summary>A priced takeoff line.</summary>
public sealed record ConstructionBudgetLine(MaterialTakeoffLine Takeoff, decimal UnitPrice, decimal Total);

/// <summary>Materials priced, plus labor and contingency as rates of the material cost.</summary>
public sealed record ConstructionBudget(BudgetSettings Settings, MaterialTakeoffResult Takeoff,
    IReadOnlyList<ConstructionBudgetLine> Lines, decimal Materials, decimal Labor, decimal Contingency, decimal Total)
{
    /// <summary>Material cost of each trade, in takeoff order.</summary>
    public IReadOnlyList<(MaterialCategory Category, decimal Total)> ByCategory() => Lines
        .GroupBy(line => line.Takeoff.Material.Category)
        .Select(group => (group.Key, group.Sum(line => line.Total)))
        .ToList();

    public string ToCsv()
    {
        static string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        var csv = new StringBuilder("Category,Code,Description,Unit,PurchaseQuantity,UnitPrice,Total\n");
        foreach (var line in Lines)
            csv.Append(line.Takeoff.Material.Category).Append(',').Append(line.Takeoff.Code).Append(',')
                .Append(SpreadsheetCsv.Quote(line.Takeoff.Material.Description)).Append(',')
                .Append(SpreadsheetCsv.Quote(line.Takeoff.Material.Unit)).Append(',')
                .Append(line.Takeoff.PurchaseQuantity.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(N(line.UnitPrice)).Append(',').AppendLine(N(line.Total));
        csv.AppendLine($"MATERIALS,,,,,,{N(Materials)}");
        csv.AppendLine($"LABOR,,,,,{Settings.LaborRate.ToString("0.###", CultureInfo.InvariantCulture)},{N(Labor)}");
        csv.AppendLine($"CONTINGENCY,,,,,{Settings.ContingencyRate.ToString("0.###", CultureInfo.InvariantCulture)},{N(Contingency)}");
        csv.AppendLine($"TOTAL_{Settings.Currency},,,,,,{N(Total)}");
        return csv.ToString();
    }

    public static ConstructionBudget Build(BuildingDocument document, BudgetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var takeoff = MaterialTakeoff.Build(document);
        var lines = takeoff.Lines.Select(line =>
        {
            var price = settings.PriceOf(line.Material);
            return new ConstructionBudgetLine(line, price, Math.Round((decimal)line.PurchaseQuantity * price, 2));
        }).ToList();
        var materials = lines.Sum(line => line.Total);
        // Furnishings are bought ready-made: they carry no site labor.
        var built = lines.Where(line => line.Takeoff.Material.Category != MaterialCategory.Furnishings).Sum(line => line.Total);
        var labor = Math.Round(built * settings.LaborRate, 2);
        var contingency = Math.Round((materials + labor) * settings.ContingencyRate, 2);
        return new ConstructionBudget(settings, takeoff, lines, materials, labor, contingency, materials + labor + contingency);
    }
}

public sealed partial class DocumentSession
{
    /// <summary>The budget settings saved in the project, or the reference settings when none were saved.</summary>
    public BudgetSettings GetBudgetSettings()
    {
        try { return BudgetSettings.FromJson(GetExtension(BudgetSettings.Key)); }
        catch (Exception exception) when (exception is JsonException or InvalidDataException) { return BudgetSettings.Default; }
    }

    /// <summary>Saves the budget settings as one undoable step.</summary>
    public void SetBudgetSettings(BudgetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        SetExtension(BudgetSettings.Key, settings.ToJson());
    }
}
