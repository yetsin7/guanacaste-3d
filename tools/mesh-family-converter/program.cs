using System.Globalization;
using System.Numerics;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Import;

/// <summary>
/// Converts a downloaded 3D model (glTF, OBJ, Collada...) into a Guanacaste 3D mesh component family (.g3df):
///   inspect &lt;model&gt;
///   convert &lt;model&gt; &lt;family.g3df&gt; --name N --category Planting|Entourage [--plan-shape Car] [--triangles 150000]
///           [--yaw degrees] [--map materialSubstring=SurfaceKind ...] [--default SurfaceKind] [--meta key=value ...]
/// The model is reduced to the triangle budget, its materials are mapped onto render surfaces, it is turned so its
/// front faces -Y, and its origin moves to the middle of its base.
/// </summary>
internal static class Program
{
    public static int Main(string[] arguments)
    {
        if (arguments.Length >= 2 && arguments[0] == "inspect")
        {
            var model = Load(arguments[1]);
            var (low, high) = model.Bounds();
            Console.WriteLine($"{model.TriangleCount:N0} triangles, bounds {low} → {high} (size {high - low})");
            for (var index = 0; index < model.Materials.Count; index++)
                Console.WriteLine($"  [{index}] {model.Materials[index].Name}: {model.TriangleMaterial.Count(material => material == index):N0} triangles, " +
                                  $"color {model.Materials[index].BaseColor}");
            return 0;
        }
        if (arguments.Length < 3 || arguments[0] != "convert")
        {
            Console.Error.WriteLine("Usage: inspect <model> | convert <model> <family.g3df> --name N --category Planting|Entourage ...");
            return 2;
        }
        var options = Options(arguments[3..]);
        var source = Load(arguments[1]);
        var budget = int.Parse(Single(options, "--triangles") ?? "150000", CultureInfo.InvariantCulture);
        var maps = options.GetValueOrDefault("--map", []).Select(rule => rule.Split('=', 2))
            .Select(pair => (Pattern: pair[0], Kind: Enum.Parse<SurfaceKind>(pair[1]))).ToArray();
        var fallback = Enum.Parse<SurfaceKind>(Single(options, "--default") ?? nameof(SurfaceKind.Structure));
        var thin = options.GetValueOrDefault("--thin", []).Select(rule => rule.Split('=', 2))
            .Select(pair => (Pattern: pair[0], Keep: double.Parse(pair[1], CultureInfo.InvariantCulture))).ToArray();
        string NameOf(int material) => material >= 0 && material < source.Materials.Count ? source.Materials[material].Name : string.Empty;
        SurfaceKind KindOf(int material)
        {
            foreach (var (pattern, kind) in maps)
                if (NameOf(material).Contains(pattern, StringComparison.OrdinalIgnoreCase)) return kind;
            return fallback;
        }

        // Thinned materials (leaves) keep a fraction of their separate pieces, each enlarged about its own center so
        // the crown covers the same area; every other material shares the triangle budget and is reduced by clustering.
        var thinned = Enumerable.Range(0, Math.Max(1, source.Materials.Count))
            .ToDictionary(material => material, material => thin.FirstOrDefault(rule =>
                NameOf(material).Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)).Keep);
        var outPositions = new List<Vector3>();
        var outIndices = new List<int>();
        var outMaterials = new List<int>();
        var solid = Subset(source, triangle => thinned.GetValueOrDefault(source.TriangleMaterial[triangle]) == 0);
        var reduced = MeshProxy.Build(solid, Math.Max(1000, budget));
        outPositions.AddRange(reduced.Positions);
        outIndices.AddRange(reduced.Indices);
        outMaterials.AddRange(reduced.TriangleMaterial);
        foreach (var (material, keep) in thinned.Where(pair => pair.Value > 0))
            Thin(source, material, keep, outPositions, outIndices, outMaterials);
        var proxy = new ProxyMesh
        {
            Positions = [.. outPositions], Indices = [.. outIndices], TriangleMaterial = [.. outMaterials],
            SourceTriangles = source.TriangleCount
        };

        // Turn about Z so the front faces -Y, then move the base center to the origin.
        var yaw = double.Parse(Single(options, "--yaw") ?? "0", CultureInfo.InvariantCulture) * Math.PI / 180;
        var (cos, sin) = ((float)Math.Cos(yaw), (float)Math.Sin(yaw));
        var turned = proxy.Positions.Select(p => new Vector3(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos, p.Z)).ToArray();
        var min = turned.Aggregate(new Vector3(float.MaxValue), Vector3.Min);
        var max = turned.Aggregate(new Vector3(float.MinValue), Vector3.Max);
        var center = new Vector3((min.X + max.X) / 2, (min.Y + max.Y) / 2, min.Z);
        var positions = turned.Select(p => p - center).ToArray();

        // Drop triangles the reduction collapsed, then share vertices and smooth normals by area.
        var indices = new List<int>();
        var kinds = new List<SurfaceKind>();
        for (var triangle = 0; triangle < proxy.TriangleCount; triangle++)
        {
            var (a, b, c) = (proxy.Indices[triangle * 3], proxy.Indices[triangle * 3 + 1], proxy.Indices[triangle * 3 + 2]);
            if (a == b || b == c || a == c) continue;
            if (Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).LengthSquared() < 1e-14f) continue;
            indices.AddRange([a, b, c]);
            kinds.Add(KindOf(proxy.TriangleMaterial[triangle]));
        }
        var normals = SmoothNormals(positions, indices);
        var family = new FamilyDocument
        {
            Name = Single(options, "--name") ?? Path.GetFileNameWithoutExtension(arguments[2]),
            Category = Enum.Parse<FamilyCategory>(Single(options, "--category") ?? nameof(FamilyCategory.Entourage)),
            Types = [new FamilyTypeDefinition(Guid.NewGuid(), "Estándar", [])],
            Mesh = FamilyMesh.Create(positions, indices, kinds, normals)
        };
        // A light version for interactive views, reduced by clustering from the converted mesh.
        var previewBudget = int.Parse(Single(options, "--preview") ?? "20000", CultureInfo.InvariantCulture);
        if (kinds.Count > previewBudget * 1.5)
        {
            var converted = new ImportedModel
            {
                Positions = positions, Indices = [.. indices], TriangleMaterial = kinds.Select(kind => (int)kind).ToArray()
            };
            var light = MeshProxy.Build(converted, previewBudget);
            var lightIndices = new List<int>();
            var lightKinds = new List<SurfaceKind>();
            for (var triangle = 0; triangle < light.TriangleCount; triangle++)
            {
                var (a, b, c) = (light.Indices[triangle * 3], light.Indices[triangle * 3 + 1], light.Indices[triangle * 3 + 2]);
                if (a == b || b == c || a == c) continue;
                lightIndices.AddRange([a, b, c]);
                lightKinds.Add((SurfaceKind)light.TriangleMaterial[triangle]);
            }
            family.Mesh.Preview = FamilyMesh.Create(light.Positions, lightIndices, lightKinds,
                SmoothNormals(light.Positions, lightIndices));
            Console.WriteLine($"  preview: {lightKinds.Count:N0} triangles");
        }
        family.Id = ArchitecturalLibrary.StableId("mesh-family", family.Name);
        if (Single(options, "--plan-shape") is { } shape) family.Metadata["PlanShape"] = shape;
        foreach (var pair in options.GetValueOrDefault("--meta", []).Select(entry => entry.Split('=', 2)))
            family.Metadata[pair[0]] = pair[1];
        NativeDocumentStore.SaveFamily(arguments[2], family);
        var reloaded = NativeDocumentStore.LoadFamily(arguments[2]);
        var size = reloaded.Mesh!.Decode().Size;
        Console.WriteLine($"{reloaded.Name}: {source.TriangleCount:N0} → {kinds.Count:N0} triangles, size {size.X:0.00} × {size.Y:0.00} × {size.Z:0.00} m, " +
                          $"{new FileInfo(arguments[2]).Length / 1024.0 / 1024:0.0} MB");
        foreach (var group in kinds.GroupBy(kind => kind).OrderByDescending(group => group.Count()))
            Console.WriteLine($"  {group.Key}: {group.Count():N0}");
        return 0;
    }

    /// <summary>The triangles that pass a test, as a model of their own with compacted vertices.</summary>
    private static ImportedModel Subset(ImportedModel model, Func<int, bool> include)
    {
        var map = new Dictionary<int, int>();
        var positions = new List<Vector3>();
        var indices = new List<int>();
        var materials = new List<int>();
        for (var triangle = 0; triangle < model.TriangleCount; triangle++)
        {
            if (!include(triangle)) continue;
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = model.Indices[triangle * 3 + corner];
                if (!map.TryGetValue(vertex, out var mapped))
                {
                    map[vertex] = mapped = positions.Count;
                    positions.Add(model.Positions[vertex]);
                }
                indices.Add(mapped);
            }
            materials.Add(model.TriangleMaterial[triangle]);
        }
        return new ImportedModel
        {
            Name = model.Name, Positions = [.. positions], Indices = [.. indices], TriangleMaterial = [.. materials],
            Materials = model.Materials
        };
    }

    /// <summary>
    /// Keeps a deterministic fraction of the connected pieces (single leaves) of one material and scales each kept piece
    /// about its centroid by 1/√fraction, so the foliage keeps its coverage with far fewer triangles.
    /// </summary>
    private static void Thin(ImportedModel model, int material, double keep, List<Vector3> positions, List<int> indices,
        List<int> materials)
    {
        var parent = new Dictionary<int, int>();
        int Find(int vertex)
        {
            if (!parent.TryGetValue(vertex, out var up)) return parent[vertex] = vertex;
            while (up != vertex)
            {
                var next = parent[up];
                parent[vertex] = next;
                vertex = up;
                up = next;
            }
            return vertex;
        }
        var triangles = Enumerable.Range(0, model.TriangleCount).Where(triangle => model.TriangleMaterial[triangle] == material).ToArray();
        foreach (var triangle in triangles)
        {
            var root = Find(model.Indices[triangle * 3]);
            for (var corner = 1; corner < 3; corner++)
            {
                var other = Find(model.Indices[triangle * 3 + corner]);
                if (other != root) parent[other] = root;
            }
        }
        var pieces = triangles.GroupBy(triangle => Find(model.Indices[triangle * 3]));
        var enlarge = (float)Math.Min(3.5, 1 / Math.Sqrt(keep));
        foreach (var piece in pieces)
        {
            // A hash of the piece decides, so the same model always thins to the same leaves.
            if ((uint)(piece.Key * 2654435761u) / (double)uint.MaxValue > keep) continue;
            var vertices = piece.SelectMany(triangle => new[] { model.Indices[triangle * 3], model.Indices[triangle * 3 + 1], model.Indices[triangle * 3 + 2] })
                .Distinct().ToArray();
            var center = vertices.Aggregate(Vector3.Zero, (sum, vertex) => sum + model.Positions[vertex]) / vertices.Length;
            var map = new Dictionary<int, int>();
            foreach (var vertex in vertices)
            {
                map[vertex] = positions.Count;
                positions.Add(center + (model.Positions[vertex] - center) * enlarge);
            }
            foreach (var triangle in piece)
            {
                for (var corner = 0; corner < 3; corner++) indices.Add(map[model.Indices[triangle * 3 + corner]]);
                materials.Add(material);
            }
        }
    }

    private static ImportedModel Load(string path) =>
        ModelImporter.Import(path, new ModelImportOptions { MetersPerUnit = 1, MaximumTriangles = 40_000_000 }).Model;

    /// <summary>Area-weighted vertex normals over shared vertices.</summary>
    private static Vector3[] SmoothNormals(Vector3[] positions, List<int> indices)
    {
        var normals = new Vector3[positions.Length];
        for (var index = 0; index < indices.Count; index += 3)
        {
            var (a, b, c) = (indices[index], indices[index + 1], indices[index + 2]);
            var face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += face;
            normals[b] += face;
            normals[c] += face;
        }
        return normals.Select(normal => normal.LengthSquared() > 0 ? Vector3.Normalize(normal) : Vector3.UnitZ).ToArray();
    }

    private static string? Single(Dictionary<string, List<string>> options, string key) =>
        options.TryGetValue(key, out var values) ? values[^1] : null;

    private static Dictionary<string, List<string>> Options(string[] arguments)
    {
        var options = new Dictionary<string, List<string>>();
        for (var index = 0; index + 1 < arguments.Length; index += 2)
        {
            if (!options.TryGetValue(arguments[index], out var values)) options[arguments[index]] = values = [];
            values.Add(arguments[index + 1]);
        }
        return options;
    }
}
