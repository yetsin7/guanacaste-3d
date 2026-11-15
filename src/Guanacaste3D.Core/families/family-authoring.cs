using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Guanacaste3D.Core;

/// <summary>
/// Building one's own families — furniture, casework, fixtures — the way the reference family editor does it: a
/// reference plane at the floor, length parameters (Ancho, Fondo, Alto), types that give them values, and solids whose
/// outline, height and start offset are numbers, parameter names or formulas. A family is loaded into a project by
/// regenerating one of its types into the mesh the project places (<see cref="Bake"/>).
/// </summary>
public static class FamilyAuthoring
{
    /// <summary>The horizontal reference plane at the family's floor, where boxes are drawn.</summary>
    public const string FloorPlaneName = "Nivel de referencia";

    /// <summary>A new, empty family of a category, with its floor reference plane and a first type.</summary>
    public static FamilyDocument Create(string name, FamilyCategory category)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) throw new InvalidDataException("The family name is not valid.");
        if (!Enum.IsDefined(category)) throw new InvalidDataException("The family category is not valid.");
        return new FamilyDocument
        {
            Name = name.Trim(),
            Category = category,
            WorkPlanes = [new FamilyWorkPlane(Guid.NewGuid(), FloorPlaneName, new(0, 0, 0), new(0, 0, 1), new(1, 0, 0))],
            Types = [new FamilyTypeDefinition(Guid.NewGuid(), "Tipo 1", [])]
        };
    }

    /// <summary>Adds a length parameter with a default value in meters; its name must be new in the family.</summary>
    public static Guid AddLengthParameter(FamilyDocument family, string name, double value, FamilyParameterScope scope = FamilyParameterScope.Type)
    {
        ArgumentNullException.ThrowIfNull(family);
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > 64 || family.Parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The parameter name is empty or already used.");
        if (!double.IsFinite(value) || value <= 0) throw new InvalidDataException("A length must be positive.");
        var id = Guid.NewGuid();
        family.Parameters.Add(new FamilyParameterDefinition(id, name, FamilyParameterKind.Length, scope) { DefaultNumber = value });
        return id;
    }

    /// <summary>Adds a type, copying the values of an existing one so it only needs the values that change.</summary>
    public static Guid AddType(FamilyDocument family, string name, Guid? copyFrom = null)
    {
        ArgumentNullException.ThrowIfNull(family);
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > 64 || family.Types.Any(type => string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The type name is empty or already used.");
        var source = family.Types.FirstOrDefault(type => type.Id == copyFrom);
        var id = Guid.NewGuid();
        family.Types.Add(new FamilyTypeDefinition(id, name, source is null ? [] : [.. source.Values]));
        return id;
    }

    /// <summary>Sets a parameter's value in one type.</summary>
    public static void SetValue(FamilyDocument family, Guid typeId, string parameter, double value)
    {
        ArgumentNullException.ThrowIfNull(family);
        var definition = family.Parameters.FirstOrDefault(candidate => string.Equals(candidate.Name, parameter, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The family parameter does not exist.");
        var index = family.Types.FindIndex(type => type.Id == typeId);
        if (index < 0) throw new InvalidDataException("A family type is invalid.");
        if (!double.IsFinite(value)) throw new InvalidDataException("A length must be positive.");
        var type = family.Types[index];
        var values = type.Values.Where(item => item.ParameterId != definition.Id).Append(new FamilyParameterValue(definition.Id, value, null)).ToList();
        family.Types[index] = type with { Values = values };
    }

    /// <summary>
    /// Adds a box on the floor reference plane: its corner (x, y), size (width, depth), start offset and height are
    /// expressions — a number such as "0.05", a parameter such as "Alto", or a formula such as "Alto - 0.03".
    /// </summary>
    public static Guid AddBox(FamilyDocument family, string name, string x, string y, string width, string depth, string offset, string height)
    {
        ArgumentNullException.ThrowIfNull(family);
        var plane = family.WorkPlanes.FirstOrDefault(candidate => candidate.Name == FloorPlaneName) ?? family.WorkPlanes.FirstOrDefault()
            ?? throw new InvalidDataException("An extrusion refers to a work plane that does not exist.");
        var id = Guid.NewGuid();
        var operation = new FamilyOperation(id, string.IsNullOrWhiteSpace(name) ? $"Caja {family.Operations.Count + 1}" : name.Trim(),
            FamilyOperationKind.SolidExtrusion)
        {
            WorkPlaneId = plane.Id,
            Expressions = new(StringComparer.Ordinal)
            {
                [FamilyGeometry.ProfileXKey] = x, [FamilyGeometry.ProfileYKey] = y,
                [FamilyGeometry.ProfileWidthKey] = width, [FamilyGeometry.ProfileDepthKey] = depth,
                [FamilyGeometry.OffsetKey] = offset, [FamilyGeometry.DepthKey] = height
            }
        };
        // The drawn outline is the first type's rectangle; the parametric one is what regenerates per type.
        var outline = FamilyGeometry.ProfileOf(family, operation with { Profile = [new(0, 0), new(1, 0), new(1, 1)] });
        family.Operations.Add(operation with { Profile = [.. outline] });
        try { family.Validate(); }
        catch { family.Operations.RemoveAll(candidate => candidate.Id == id); throw; }
        return id;
    }

    /// <summary>
    /// Adds a solid turned about a vertical axis — a lamp, a vase, a turned leg. The profile is a closed outline of
    /// (distance from the axis, height) points, never crossing the axis; the axis stands at (axisX, axisY), which may be
    /// numbers, parameters or formulas.
    /// </summary>
    public static Guid AddRevolution(FamilyDocument family, string name, IReadOnlyList<Point2> profile, string axisX = "0", string axisY = "0",
        string material = "")
    {
        ArgumentNullException.ThrowIfNull(family);
        var plane = FloorPlane(family);
        var id = Guid.NewGuid();
        family.Operations.Add(new FamilyOperation(id, string.IsNullOrWhiteSpace(name) ? $"Revolución {family.Operations.Count + 1}" : name.Trim(),
            FamilyOperationKind.SolidRevolution)
        {
            WorkPlaneId = plane.Id,
            Profile = [.. profile],
            Expressions = new(StringComparer.Ordinal) { [FamilyGeometry.AxisXKey] = axisX, [FamilyGeometry.AxisYKey] = axisY, [FamilyGeometry.MaterialKey] = MaterialName(material) }
        });
        return Validated(family, id);
    }

    /// <summary>Adds an extrusion of a drawn outline (any closed shape) from a start height, with a height expression.</summary>
    public static Guid AddExtrusion(FamilyDocument family, string name, IReadOnlyList<Point2> outline, string offset, string height, string material = "")
    {
        ArgumentNullException.ThrowIfNull(family);
        var plane = FloorPlane(family);
        var id = Guid.NewGuid();
        family.Operations.Add(new FamilyOperation(id, string.IsNullOrWhiteSpace(name) ? $"Extrusión {family.Operations.Count + 1}" : name.Trim(),
            FamilyOperationKind.SolidExtrusion)
        {
            WorkPlaneId = plane.Id,
            Profile = [.. outline],
            Expressions = new(StringComparer.Ordinal) { [FamilyGeometry.OffsetKey] = offset, [FamilyGeometry.DepthKey] = height, [FamilyGeometry.MaterialKey] = MaterialName(material) }
        });
        return Validated(family, id);
    }

    /// <summary>A solid with no material named is painted white; the model never stores an empty expression.</summary>
    private static string MaterialName(string? material) => string.IsNullOrWhiteSpace(material) ? "Pintura blanca" : material.Trim();

    /// <summary>Sets the material of a solid by its name in <see cref="FamilyGeometry.Materials"/>.</summary>
    public static void SetMaterial(FamilyDocument family, Guid operationId, string material)
    {
        ArgumentNullException.ThrowIfNull(family);
        var index = family.Operations.FindIndex(operation => operation.Id == operationId);
        if (index < 0) throw new InvalidDataException("The family solid does not exist.");
        if (!string.IsNullOrWhiteSpace(material) && !FamilyGeometry.Materials.ContainsKey(material.Trim()))
            throw new InvalidDataException("The material of a family solid is not known.");
        var operation = family.Operations[index];
        family.Operations[index] = operation with
        {
            Expressions = new Dictionary<string, string>(operation.Expressions, StringComparer.Ordinal) { [FamilyGeometry.MaterialKey] = MaterialName(material) }
        };
    }

    /// <summary>
    /// Reads an outline typed as points separated by semicolons, each point two numbers separated by spaces:
    /// "0 0; 0,3 0; 0,3 0,05" — the decimal comma and the decimal point both work.
    /// </summary>
    public static List<Point2> ParseOutline(string text)
    {
        var points = new List<Point2>();
        foreach (var part in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var numbers = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (numbers.Length != 2 ||
                !double.TryParse(numbers[0].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(numbers[1].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
                throw new InvalidDataException("Write the outline as points «x y» separated by semicolons.");
            points.Add(new Point2(x, y));
        }
        if (points.Count < 3) throw new InvalidDataException("An outline needs at least three points.");
        return points;
    }

    /// <summary>Writes an outline the way <see cref="ParseOutline"/> reads it, in the interface's number format.</summary>
    public static string FormatOutline(IReadOnlyList<Point2> points) => string.Join("; ", points.Select(point =>
        $"{point.X.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture)} {point.Y.ToString("0.###", System.Globalization.CultureInfo.CurrentCulture)}"));

    private static FamilyWorkPlane FloorPlane(FamilyDocument family) =>
        family.WorkPlanes.FirstOrDefault(candidate => candidate.Name == FloorPlaneName) ?? family.WorkPlanes.FirstOrDefault()
        ?? throw new InvalidDataException("An extrusion refers to a work plane that does not exist.");

    private static Guid Validated(FamilyDocument family, Guid id)
    {
        try { family.Validate(); }
        catch { family.Operations.RemoveAll(candidate => candidate.Id == id); throw; }
        return id;
    }

    /// <summary>
    /// The family as a project loads one of its types: its solids regenerated for that type, centred on the insertion
    /// point with its base on the floor, under a stable id per family and type so loading it again replaces it.
    /// </summary>
    public static FamilyDocument Bake(FamilyDocument family, Guid typeId)
    {
        ArgumentNullException.ThrowIfNull(family);
        family.Validate();
        var type = family.Types.FirstOrDefault(candidate => candidate.Id == typeId) ?? throw new InvalidDataException("A family type is invalid.");
        var mesh = FamilyGeometry.Tessellate(family, typeId);
        if (mesh.TriangleCount == 0) throw new InvalidDataException("The family has no solids to place.");
        var min = new Vector3(mesh.Positions.Min(point => point.X), mesh.Positions.Min(point => point.Y), mesh.Positions.Min(point => point.Z));
        var max = new Vector3(mesh.Positions.Max(point => point.X), mesh.Positions.Max(point => point.Y), mesh.Positions.Max(point => point.Z));
        var shift = new Vector3((min.X + max.X) / 2, (min.Y + max.Y) / 2, min.Z);
        var baked = family.Clone();
        baked.Id = StableId(family.Id, typeId);
        baked.Name = family.Types.Count > 1 ? $"{family.Name} : {type.Name}" : family.Name;
        baked.Types = [type];
        baked.Metadata["SourceFamily"] = family.Id.ToString();
        baked.Metadata["SourceType"] = typeId.ToString();
        baked.Mesh = FamilyMesh.Create(mesh.Positions.Select(point => point - shift).ToList(), mesh.Indices, mesh.TriangleKinds);
        baked.Validate();
        return baked;
    }

    private static Guid StableId(Guid family, Guid type)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{family:N}:{type:N}"));
        return new Guid(bytes);
    }
}
