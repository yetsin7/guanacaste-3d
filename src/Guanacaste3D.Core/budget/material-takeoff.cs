using System.Globalization;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>Trade a construction material belongs to, in the order a house is built.</summary>
public enum MaterialCategory
{
    Foundations,
    Structure,
    Masonry,
    Drywall,
    Plaster,
    Roofing,
    Ceilings,
    Floors,
    Paint,
    DoorsAndWindows
}

/// <summary>One material of the catalog: a stable code, how it is sold and how much is lost on site.</summary>
public sealed record MaterialDefinition(string Code, MaterialCategory Category, string Description, string Unit,
    double WasteRate, bool SoldWhole);

/// <summary>
/// One line of the takeoff. <see cref="NetQuantity"/> is what the model needs; <see cref="PurchaseQuantity"/> adds
/// the waste and rounds to what can be bought. <see cref="Sources"/> says which elements produced it.
/// </summary>
public sealed record MaterialTakeoffLine(MaterialDefinition Material, double NetQuantity, double PurchaseQuantity,
    IReadOnlyList<string> Sources)
{
    public string Code => Material.Code;
}

/// <summary>A purchase-ready list of materials derived from the model, ordered by trade.</summary>
public sealed record MaterialTakeoffResult(IReadOnlyList<MaterialTakeoffLine> Lines, IReadOnlyList<string> Assumptions)
{
    public MaterialTakeoffLine? Find(string code) => Lines.FirstOrDefault(line => line.Code == code);

    /// <summary>Exports the takeoff as invariant-culture CSV for a spreadsheet.</summary>
    public string ToCsv()
    {
        var csv = new StringBuilder("Category,Code,Description,Unit,NetQuantity,WasteRate,PurchaseQuantity,Sources\n");
        foreach (var line in Lines)
            csv.Append(line.Material.Category).Append(',').Append(line.Code).Append(',')
                .Append(SpreadsheetCsv.Quote(line.Material.Description)).Append(',')
                .Append(SpreadsheetCsv.Quote(line.Material.Unit)).Append(',')
                .Append(line.NetQuantity.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(line.Material.WasteRate.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(line.PurchaseQuantity.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .AppendLine(SpreadsheetCsv.Quote(string.Join("; ", line.Sources)));
        return csv.ToString();
    }
}

/// <summary>
/// The material catalog and the yields used to turn model quantities into materials. The values are common
/// Costa Rican residential practice (block 12×20×40, concrete 210 kg/cm², galvanised roof sheet) and are reference
/// values: a project with other systems changes the catalog, not the model.
/// </summary>
public static class MaterialCatalog
{
    public const string Block12 = "BLOCK_12";
    public const string Block15 = "BLOCK_15";
    public const string Block20 = "BLOCK_20";
    public const string Brick = "BRICK";
    public const string Cement = "CEMENT_BAG";
    public const string Sand = "SAND";
    public const string Stone = "STONE";
    public const string BaseGravel = "BASE_GRAVEL";
    public const string Rebar2 = "REBAR_2";
    public const string Rebar3 = "REBAR_3";
    public const string Rebar4 = "REBAR_4";
    public const string TieWire = "TIE_WIRE";
    public const string Mesh = "WELDED_MESH";
    public const string FinePlaster = "FINE_PLASTER_BAG";
    public const string GypsumBoard = "GYPSUM_BOARD";
    public const string Stud = "STEEL_STUD";
    public const string Track = "STEEL_TRACK";
    public const string DrywallScrew = "DRYWALL_SCREW";
    public const string FramingScrew = "FRAMING_SCREW";
    public const string JointCompound = "JOINT_COMPOUND";
    public const string JointTape = "JOINT_TAPE";
    public const string GlassPartition = "GLASS_PARTITION";
    public const string Paint = "PAINT_GALLON";
    public const string Ceramic = "CERAMIC_TILE";
    public const string TileAdhesive = "TILE_ADHESIVE";
    public const string Grout = "GROUT_BAG";
    public const string RoofSheet = "ROOF_SHEET";
    public const string Purlin = "PURLIN";
    public const string RoofScrew = "ROOF_SCREW";
    public const string CeilingBoard = "CEILING_BOARD";
    public const string CeilingProfile = "CEILING_PROFILE";
    public const string Door = "DOOR";
    public const string Hinge = "HINGE";
    public const string Lockset = "LOCKSET";
    public const string Window = "WINDOW_AREA";

    // Yields. Everything is metric; a "bag" of cement is 50 kg.
    public const double BlocksPerSquareMetre = 12.5;
    public const double BricksPerSquareMetre = 40;
    public const double MortarCementBagsPerCubicMetre = 8.0;
    public const double MortarSandPerCubicMetre = 1.1;
    public const double ConcreteCementBagsPerCubicMetre = 8.5;
    public const double ConcreteSandPerCubicMetre = 0.55;
    public const double ConcreteStonePerCubicMetre = 0.75;
    public const double BarLength = 6.0;
    public const double MeshSheetArea = 2.35 * 6.0 * 0.85;
    public const double BoardArea = 1.22 * 2.44;
    public const double StudLength = 3.05;
    public const double DrywallScrewsPerSquareMetre = 30;
    public const double SquareMetresPerCompoundBucket = 25;
    public const double TapeRollLength = 75;
    public const double SquareMetresPerGallonPerCoat = 35;
    public const int PaintCoats = 2;
    public const double SquareMetresPerAdhesiveBag = 4;
    public const double SquareMetresPerGroutBag = 5;
    public const double RoofSheetCover = 0.80 * 3.50;
    public const double PurlinSpacing = 0.90;
    public const double RoofScrewsPerSquareMetre = 6;
    public const double CeilingProfileMetresPerSquareMetre = 3.0;
    public const double CeilingProfileLength = 3.66;
    public const double FinePlasterSquareMetresPerBag = 4;
    public const double AssumedRepelloThickness = 0.015;

    private static readonly MaterialDefinition[] All =
    [
        new(Rebar4, MaterialCategory.Foundations, "Varilla corrugada #4 (12 mm)", "varilla 6 m", 0.05, true),
        new(BaseGravel, MaterialCategory.Foundations, "Lastre compactado para base de contrapiso", "m³", 0.15, false),
        new(Cement, MaterialCategory.Structure, "Cemento gris", "saco 50 kg", 0.05, true),
        new(Sand, MaterialCategory.Structure, "Arena de tajo", "m³", 0.10, false),
        new(Stone, MaterialCategory.Structure, "Piedra quintilla", "m³", 0.10, false),
        new(Rebar3, MaterialCategory.Structure, "Varilla corrugada #3 (9.5 mm)", "varilla 6 m", 0.05, true),
        new(Rebar2, MaterialCategory.Structure, "Varilla #2 (6 mm) para aros", "varilla 6 m", 0.05, true),
        new(TieWire, MaterialCategory.Structure, "Alambre negro de amarre", "kg", 0.0, true),
        new(Mesh, MaterialCategory.Structure, "Malla electrosoldada 2.35 × 6 m", "lámina", 0.0, true),
        new(Block12, MaterialCategory.Masonry, "Bloque de concreto 12 × 20 × 40", "u", 0.05, true),
        new(Block15, MaterialCategory.Masonry, "Bloque de concreto 15 × 20 × 40", "u", 0.05, true),
        new(Block20, MaterialCategory.Masonry, "Bloque de concreto 20 × 20 × 40", "u", 0.05, true),
        new(Brick, MaterialCategory.Masonry, "Ladrillo de barro", "u", 0.08, true),
        new(GypsumBoard, MaterialCategory.Drywall, "Lámina de yeso 1.22 × 2.44", "lámina", 0.10, true),
        new(Stud, MaterialCategory.Drywall, "Poste metálico 3.05 m", "u", 0.05, true),
        new(Track, MaterialCategory.Drywall, "Canal (riel) metálico 3.05 m", "u", 0.05, true),
        new(DrywallScrew, MaterialCategory.Drywall, "Tornillo punta fina 1\" para lámina", "u", 0.10, true),
        new(FramingScrew, MaterialCategory.Drywall, "Tornillo framer 7/16\" para estructura", "u", 0.10, true),
        new(JointCompound, MaterialCategory.Drywall, "Pasta para juntas", "cubeta", 0.0, true),
        new(JointTape, MaterialCategory.Drywall, "Cinta de papel para juntas", "rollo 75 m", 0.0, true),
        new(GlassPartition, MaterialCategory.Drywall, "Mampara de vidrio templado con perfilería", "m²", 0.0, false),
        new(FinePlaster, MaterialCategory.Plaster, "Fino (repello fino) premezclado", "saco 40 kg", 0.10, true),
        new(RoofSheet, MaterialCategory.Roofing, "Lámina de techo HG ondulada 3.66 m", "lámina", 0.08, true),
        new(Purlin, MaterialCategory.Roofing, "Perlin (RT) 6 m", "u", 0.05, true),
        new(RoofScrew, MaterialCategory.Roofing, "Tornillo de techo con arandela", "u", 0.10, true),
        new(CeilingBoard, MaterialCategory.Ceilings, "Lámina de cielo (fibrocemento)", "lámina", 0.10, true),
        new(CeilingProfile, MaterialCategory.Ceilings, "Perfil para cielo 3.66 m", "u", 0.05, true),
        new(Ceramic, MaterialCategory.Floors, "Cerámica para piso", "m²", 0.10, false),
        new(TileAdhesive, MaterialCategory.Floors, "Pegamento para cerámica", "saco 20 kg", 0.05, true),
        new(Grout, MaterialCategory.Floors, "Fragua (boquilla)", "saco 2 kg", 0.05, true),
        new(Paint, MaterialCategory.Paint, "Pintura látex", "galón", 0.05, true),
        new(Door, MaterialCategory.DoorsAndWindows, "Puerta con marco, colocada", "u", 0.0, true),
        new(Hinge, MaterialCategory.DoorsAndWindows, "Bisagra 3½\"", "u", 0.0, true),
        new(Lockset, MaterialCategory.DoorsAndWindows, "Cerradura (llavín)", "u", 0.0, true),
        new(Window, MaterialCategory.DoorsAndWindows, "Ventana de aluminio y vidrio", "m²", 0.0, false),
    ];

    private static readonly Dictionary<string, MaterialDefinition> ByCode = All.ToDictionary(m => m.Code, StringComparer.Ordinal);

    public static IReadOnlyList<MaterialDefinition> Materials => All;

    public static MaterialDefinition Get(string code) =>
        ByCode.TryGetValue(code, out var material) ? material : throw new KeyNotFoundException($"Unknown material '{code}'.");
}

/// <summary>Turns the walls, slabs, roofs, structure and openings of a document into a purchase list.</summary>
public static class MaterialTakeoff
{
    public static MaterialTakeoffResult Build(BuildingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        var acc = new Accumulator();
        var assumptions = new SortedSet<string>(StringComparer.Ordinal);
        var types = document.WallTypes.ToDictionary(type => type.Id);
        var openings = document.Openings.ToLookup(opening => opening.WallId);
        var levelNames = document.Levels.ToDictionary(level => level.Id, level => level.Name);
        var lowestLevel = document.Levels.OrderBy(level => level.Elevation).Select(level => level.Id).FirstOrDefault();

        foreach (var wall in document.Walls)
        {
            var type = types[wall.WallTypeId];
            var hosted = openings[wall.Id].ToArray();
            var area = WallGeometry.Solids(wall, hosted).Sum(part => (part.To - part.From) * (part.Top - part.Bottom));
            if (area <= 0) continue;
            AddWall(acc, assumptions, wall, type, area, $"Muro {type.Name} ({Name(levelNames, wall.LevelId)})");
        }

        foreach (var slab in document.Floors)
        {
            var source = $"{slab.Name} ({Name(levelNames, slab.LevelId)})";
            switch (slab.Kind)
            {
                case FloorKind.Floor:
                    AddFloor(acc, slab, slab.LevelId == lowestLevel, source);
                    break;
                case FloorKind.Ceiling:
                    AddCeiling(acc, slab.CalculateArea(), source);
                    break;
                case FloorKind.Roof when slab.IsPitched:
                    AddSheetRoof(acc, RoofGeometry.SurfaceArea(slab), source);
                    break;
                case FloorKind.Roof:
                    AddConcrete(acc, slab.CalculateArea() * slab.Thickness, source);
                    acc.Add(MaterialCatalog.Mesh, slab.CalculateArea() / MaterialCatalog.MeshSheetArea, source);
                    break;
            }
        }

        foreach (var column in document.Columns)
        {
            var source = $"Columna ({Name(levelNames, column.LevelId)})";
            AddConcrete(acc, column.Width * column.Depth * column.Height, source);
            AddFrame(acc, column.Height, 2 * (column.Width + column.Depth), 0.15, source);
        }

        foreach (var element in document.StructuralElements)
        {
            var source = $"{element.Name} ({Name(levelNames, element.LevelId)})";
            switch (element.Kind)
            {
                case StructuralElementKind.Beam:
                    AddConcrete(acc, element.Length * element.Width * element.Height, source);
                    AddFrame(acc, element.Length, 2 * (element.Width + element.Height), 0.20, source);
                    break;
                case StructuralElementKind.WallFoundation:
                    AddConcrete(acc, element.Length * element.Width * element.Height, source);
                    AddFrame(acc, element.Length, 2 * (element.Width + element.Height), 0.20, source);
                    break;
                case StructuralElementKind.IsolatedFoundation:
                    var depth = element.Depth > 0 ? element.Depth : element.Width;
                    AddConcrete(acc, element.Width * depth * element.Height, source);
                    var barsEachWay = Math.Floor(element.Width / 0.15) + 1;
                    acc.Add(MaterialCatalog.Rebar4, 2 * barsEachWay * Math.Max(element.Width, depth) / MaterialCatalog.BarLength, source);
                    break;
            }
        }

        foreach (var opening in document.Openings)
        {
            if (opening.Kind == OpeningKind.Door)
            {
                var source = $"Puerta {opening.Width:0.00} × {opening.Height:0.00}";
                acc.Add(MaterialCatalog.Door, 1, source);
                acc.Add(MaterialCatalog.Hinge, 3, source);
                acc.Add(MaterialCatalog.Lockset, 1, source);
            }
            else
            {
                acc.Add(MaterialCatalog.Window, opening.Width * opening.Height, $"Ventana {opening.Width:0.00} × {opening.Height:0.00}");
            }
        }

        // Tie wire: about one kilogram for every ten bars placed.
        var bars = acc.Net(MaterialCatalog.Rebar2) + acc.Net(MaterialCatalog.Rebar3) + acc.Net(MaterialCatalog.Rebar4);
        if (bars > 0) acc.Add(MaterialCatalog.TieWire, bars / 10, "Amarres del acero");

        var lines = acc.Lines()
            .OrderBy(line => line.Material.Category)
            .ThenBy(line => Array.IndexOf(MaterialCatalog.Materials.ToArray(), line.Material))
            .ToList();
        return new MaterialTakeoffResult(lines, assumptions.ToList());
    }

    private static void AddWall(Accumulator acc, ISet<string> assumptions, Wall wall, WallType type, double area, string source)
    {
        var assembly = WallAssemblyPlanRules.For(type, wall.Thickness);
        var leftRepello = assembly.LeftPlaster;
        var rightRepello = assembly.RightPlaster;
        var paintFaces = 0;

        switch (assembly.Construction)
        {
            case WallConstruction.Gypsum:
                AddDrywall(acc, wall, assembly.StudSpacing, area, source);
                paintFaces = 2;
                break;
            case WallConstruction.Glass:
                acc.Add(MaterialCatalog.GlassPartition, area, source);
                return;
            case WallConstruction.Brick:
                acc.Add(MaterialCatalog.Brick, area * MaterialCatalog.BricksPerSquareMetre, source);
                AddMortar(acc, area * 0.025, source);
                paintFaces = 1;
                break;
            case WallConstruction.Concrete:
                AddConcrete(acc, area * assembly.CoreThickness, source);
                AddWallReinforcement(acc, area, source);
                paintFaces = 1;
                break;
            default:
                // Plaster (rendered masonry), Block and Stone are all laid as concrete block in Costa Rican practice.
                AddBlockWall(acc, assembly.CoreThickness, area, source);
                paintFaces = assembly.Construction == WallConstruction.Plaster ? 2 : 1;
                if (assembly.Construction == WallConstruction.Plaster && leftRepello + rightRepello <= 0)
                {
                    leftRepello = rightRepello = MaterialCatalog.AssumedRepelloThickness;
                    assumptions.Add("Muros repellados sin capas modeladas: se asumió repello de 1.5 cm por cara.");
                }
                break;
        }

        var repelloVolume = area * (leftRepello + rightRepello);
        if (repelloVolume > 0) AddMortar(acc, repelloVolume, source + " · repello");
        var fineFaces = (assembly.LeftFine > 0 ? 1 : 0) + (assembly.RightFine > 0 ? 1 : 0);
        if (fineFaces == 0 && assembly.Construction == WallConstruction.Plaster) fineFaces = 2;
        if (fineFaces > 0 && assembly.Construction != WallConstruction.Gypsum)
            acc.Add(MaterialCatalog.FinePlaster, area * fineFaces / MaterialCatalog.FinePlasterSquareMetresPerBag, source + " · fino");
        if (paintFaces > 0) AddPaint(acc, area * paintFaces, source);
    }

    private static void AddBlockWall(Accumulator acc, double core, double area, string source)
    {
        var (code, mortarPerSquareMetre, groutPerSquareMetre) = core switch
        {
            < 0.135 => (MaterialCatalog.Block12, 0.010, 0.008),
            < 0.175 => (MaterialCatalog.Block15, 0.013, 0.010),
            _ => (MaterialCatalog.Block20, 0.017, 0.014)
        };
        acc.Add(code, area * MaterialCatalog.BlocksPerSquareMetre, source);
        AddMortar(acc, area * mortarPerSquareMetre, source);
        AddConcrete(acc, area * groutPerSquareMetre, source + " · relleno de celdas");
        AddWallReinforcement(acc, area, source);
    }

    /// <summary>Vertical #3 every 60 cm and horizontal #3 every third course (60 cm).</summary>
    private static void AddWallReinforcement(Accumulator acc, double area, string source) =>
        acc.Add(MaterialCatalog.Rebar3, area * (2 / 0.60) / MaterialCatalog.BarLength, source);

    private static void AddDrywall(Accumulator acc, Wall wall, double spacing, double area, string source)
    {
        var studsAlong = WallAssemblyPlanRules.StudPositions(wall.Length, Math.Clamp(spacing, 0.15, 1.2)).Count;
        var piecesPerStud = Math.Ceiling(wall.NetHeight / MaterialCatalog.StudLength);
        var studs = studsAlong * piecesPerStud;
        var boardArea = area * 2;
        acc.Add(MaterialCatalog.GypsumBoard, boardArea / MaterialCatalog.BoardArea, source);
        acc.Add(MaterialCatalog.Stud, studs, source);
        acc.Add(MaterialCatalog.Track, 2 * wall.Length / MaterialCatalog.StudLength, source);
        acc.Add(MaterialCatalog.DrywallScrew, boardArea * MaterialCatalog.DrywallScrewsPerSquareMetre, source);
        acc.Add(MaterialCatalog.FramingScrew, studs * 4, source);
        acc.Add(MaterialCatalog.JointCompound, boardArea / MaterialCatalog.SquareMetresPerCompoundBucket, source);
        acc.Add(MaterialCatalog.JointTape, boardArea * 1.3 / MaterialCatalog.TapeRollLength, source);
    }

    private static void AddFloor(Accumulator acc, FloorSlab slab, bool onGround, string source)
    {
        var area = slab.CalculateArea();
        AddConcrete(acc, area * slab.Thickness, source);
        acc.Add(MaterialCatalog.Mesh, area / MaterialCatalog.MeshSheetArea, source);
        if (onGround) acc.Add(MaterialCatalog.BaseGravel, area * 0.10, source);
        if (slab.Finish is FinishKind.Ceramic or FinishKind.Tile)
        {
            acc.Add(MaterialCatalog.Ceramic, area, source);
            acc.Add(MaterialCatalog.TileAdhesive, area / MaterialCatalog.SquareMetresPerAdhesiveBag, source);
            acc.Add(MaterialCatalog.Grout, area / MaterialCatalog.SquareMetresPerGroutBag, source);
        }
    }

    private static void AddCeiling(Accumulator acc, double area, string source)
    {
        acc.Add(MaterialCatalog.CeilingBoard, area / MaterialCatalog.BoardArea, source);
        acc.Add(MaterialCatalog.CeilingProfile, area * MaterialCatalog.CeilingProfileMetresPerSquareMetre / MaterialCatalog.CeilingProfileLength, source);
        AddPaint(acc, area, source);
    }

    private static void AddSheetRoof(Accumulator acc, double area, string source)
    {
        acc.Add(MaterialCatalog.RoofSheet, area / MaterialCatalog.RoofSheetCover, source);
        acc.Add(MaterialCatalog.Purlin, area / MaterialCatalog.PurlinSpacing / MaterialCatalog.BarLength, source);
        acc.Add(MaterialCatalog.RoofScrew, area * MaterialCatalog.RoofScrewsPerSquareMetre, source);
    }

    /// <summary>Four #4 longitudinal bars and #2 stirrups at the given spacing.</summary>
    private static void AddFrame(Accumulator acc, double length, double perimeter, double stirrupSpacing, string source)
    {
        acc.Add(MaterialCatalog.Rebar4, 4 * length * 1.10 / MaterialCatalog.BarLength, source);
        var stirrups = Math.Floor(length / stirrupSpacing) + 1;
        acc.Add(MaterialCatalog.Rebar2, stirrups * Math.Max(0.3, perimeter - 0.10) / MaterialCatalog.BarLength, source);
    }

    private static void AddConcrete(Accumulator acc, double volume, string source)
    {
        if (volume <= 0) return;
        acc.Add(MaterialCatalog.Cement, volume * MaterialCatalog.ConcreteCementBagsPerCubicMetre, source);
        acc.Add(MaterialCatalog.Sand, volume * MaterialCatalog.ConcreteSandPerCubicMetre, source);
        acc.Add(MaterialCatalog.Stone, volume * MaterialCatalog.ConcreteStonePerCubicMetre, source);
    }

    private static void AddMortar(Accumulator acc, double volume, string source)
    {
        if (volume <= 0) return;
        acc.Add(MaterialCatalog.Cement, volume * MaterialCatalog.MortarCementBagsPerCubicMetre, source);
        acc.Add(MaterialCatalog.Sand, volume * MaterialCatalog.MortarSandPerCubicMetre, source);
    }

    private static void AddPaint(Accumulator acc, double area, string source) =>
        acc.Add(MaterialCatalog.Paint, area * MaterialCatalog.PaintCoats / MaterialCatalog.SquareMetresPerGallonPerCoat, source);

    private static string Name(Dictionary<Guid, string> levels, Guid id) => levels.GetValueOrDefault(id, "?");

    /// <summary>Sums quantities by material code and remembers which elements contributed.</summary>
    private sealed class Accumulator
    {
        private readonly Dictionary<string, (double Quantity, SortedSet<string> Sources)> _items = new(StringComparer.Ordinal);

        public void Add(string code, double quantity, string source)
        {
            if (!double.IsFinite(quantity) || quantity <= 0) return;
            if (!_items.TryGetValue(code, out var item)) item = (0, new SortedSet<string>(StringComparer.CurrentCulture));
            item.Sources.Add(source);
            _items[code] = (item.Quantity + quantity, item.Sources);
        }

        public double Net(string code) => _items.TryGetValue(code, out var item) ? item.Quantity : 0;

        public IEnumerable<MaterialTakeoffLine> Lines() => _items.Select(pair =>
        {
            var material = MaterialCatalog.Get(pair.Key);
            var net = Math.Round(pair.Value.Quantity, 3);
            return new MaterialTakeoffLine(material, net, PurchaseRules.Quantity(material, net), pair.Value.Sources.ToList());
        });
    }
}

/// <summary>How a net quantity becomes a quantity that can be bought.</summary>
public static class PurchaseRules
{
    /// <summary>Adds the material's waste and rounds the result to a buyable amount.</summary>
    public static double Quantity(MaterialDefinition material, double net)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (net <= 0) return 0;
        var withWaste = net * (1 + material.WasteRate);
        // Bags, sheets, bars and pieces are bought whole; bulk materials (m³, m²) to the hundredth.
        return material.SoldWhole ? Math.Ceiling(withWaste - 1e-9) : Math.Round(withWaste, 2);
    }
}
