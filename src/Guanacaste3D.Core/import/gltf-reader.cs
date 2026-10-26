using System.Numerics;
using System.Text;
using System.Text.Json;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Core.Import;

/// <summary>
/// Reads glTF 2.0 models (.glb binary and .gltf with embedded or sibling buffers). Node hierarchies and instanced
/// meshes are expanded, vertex normals are kept for smooth shading, and PBR materials (base color, metalness,
/// roughness, emission, alpha blending) map onto renderer materials. glTF is Y-up and metric.
/// </summary>
internal static class GltfReader
{
    private const uint JsonChunk = 0x4E4F534A;
    private const uint BinaryChunk = 0x004E4942;

    public static ImportedModel Read(string path, ModelImportOptions options, IProgress<ImportProgress>? progress,
        CancellationToken cancellation)
    {
        var bytes = File.ReadAllBytes(path);
        string json;
        byte[]? embedded = null;
        if (bytes.Length >= 12 && BitConverter.ToUInt32(bytes, 0) == 0x46546C67)
        {
            var offset = 12;
            var jsonText = "";
            while (offset + 8 <= bytes.Length)
            {
                var length = (int)BitConverter.ToUInt32(bytes, offset);
                var type = BitConverter.ToUInt32(bytes, offset + 4);
                offset += 8;
                if (length < 0 || offset + length > bytes.Length) throw new InvalidDataException("The glTF file is not valid.");
                if (type == JsonChunk) jsonText = Encoding.UTF8.GetString(bytes, offset, length);
                else if (type == BinaryChunk && embedded is null) embedded = bytes.AsSpan(offset, length).ToArray();
                offset += (length + 3) & ~3;
            }
            json = jsonText;
        }
        else
        {
            json = Encoding.UTF8.GetString(bytes);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The glTF file is not valid.");
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("asset", out _))
                throw new InvalidDataException("The glTF file is not valid.");
            var context = new Context(root, embedded, Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", options);
            var mesh = new MeshAccumulator { Normals = new ArrayBuilder<Vector3>(1 << 14) };
            context.Build(mesh, cancellation);
            progress?.Report(new ImportProgress(0.95, "Import.Finishing"));
            if (mesh.TriangleCount == 0) throw new InvalidDataException("The model file contains no geometry.");
            if (context.UsedDraco) mesh.Warn("Import.Warning.GltfDraco");
            return mesh.Build(path, "glTF", ModelFrame.YUp, options.MetersPerUnit ?? 1, options);
        }
    }

    private sealed class Context(JsonElement root, byte[]? embedded, string directory, ModelImportOptions options)
    {
        private readonly Dictionary<int, byte[]> _buffers = [];
        private readonly Dictionary<int, int> _materials = [];
        public bool UsedDraco { get; private set; }

        public void Build(MeshAccumulator mesh, CancellationToken cancellation)
        {
            var nodes = Array("nodes");
            var scenes = Array("scenes");
            var roots = new List<int>();
            var sceneIndex = root.TryGetProperty("scene", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
            if (scenes.Count > sceneIndex && scenes[sceneIndex].TryGetProperty("nodes", out var sceneNodes))
                roots.AddRange(sceneNodes.EnumerateArray().Select(n => n.GetInt32()));
            else if (nodes.Count > 0)
            {
                // No scene: treat every node that is nobody's child as a root; with no nodes at all, draw every mesh.
                var children = nodes.SelectMany(n => n.TryGetProperty("children", out var c) ? c.EnumerateArray().Select(x => x.GetInt32()) : []).ToHashSet();
                roots.AddRange(Enumerable.Range(0, nodes.Count).Where(i => !children.Contains(i)));
            }
            if (nodes.Count == 0)
                for (var m = 0; m < Array("meshes").Count; m++) AddMesh(mesh, m, Matrix4x4.Identity);
            foreach (var node in roots) Visit(mesh, nodes, node, Matrix4x4.Identity, 0, cancellation);
        }

        private readonly Dictionary<string, List<JsonElement>> _arrays = [];

        /// <summary>A top-level array of the document, materialized once so indexed lookups stay cheap.</summary>
        private List<JsonElement> Array(string name)
        {
            if (_arrays.TryGetValue(name, out var cached)) return cached;
            return _arrays[name] = root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().ToList()
                : [];
        }

        private void Visit(MeshAccumulator mesh, List<JsonElement> nodes, int index, Matrix4x4 parent, int depth,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (depth > 64 || (uint)index >= (uint)nodes.Count) return;
            var node = nodes[index];
            var world = LocalMatrix(node) * parent;
            if (node.TryGetProperty("mesh", out var meshIndex) && meshIndex.ValueKind == JsonValueKind.Number)
                AddMesh(mesh, meshIndex.GetInt32(), world);
            if (node.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray()) Visit(mesh, nodes, child.GetInt32(), world, depth + 1, cancellation);
        }

        private static Matrix4x4 LocalMatrix(JsonElement node)
        {
            if (node.TryGetProperty("matrix", out var matrix) && matrix.GetArrayLength() == 16)
            {
                var m = matrix.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                // Both are column-vector data written column by column, which is the row-vector layout read row by row.
                return new Matrix4x4(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]);
            }
            var scale = node.TryGetProperty("scale", out var sc) ? ToVector(sc) : Vector3.One;
            var translation = node.TryGetProperty("translation", out var t) ? ToVector(t) : Vector3.Zero;
            var rotation = Quaternion.Identity;
            if (node.TryGetProperty("rotation", out var r) && r.GetArrayLength() == 4)
            {
                var q = r.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                rotation = Quaternion.Normalize(new Quaternion(q[0], q[1], q[2], q[3]));
            }
            return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
        }

        private static Vector3 ToVector(JsonElement array)
        {
            var v = array.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            return new Vector3(v[0], v[1], v[2]);
        }

        private void AddMesh(MeshAccumulator mesh, int meshIndex, Matrix4x4 world)
        {
            var meshes = Array("meshes");
            if ((uint)meshIndex >= (uint)meshes.Count || !meshes[meshIndex].TryGetProperty("primitives", out var primitives)) return;
            foreach (var primitive in primitives.EnumerateArray())
            {
                if (primitive.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("KHR_draco_mesh_compression", out _))
                {
                    UsedDraco = true;
                    continue;
                }
                var mode = primitive.TryGetProperty("mode", out var modeValue) ? modeValue.GetInt32() : 4;
                if (mode is not (4 or 5 or 6) || !primitive.TryGetProperty("attributes", out var attributes) ||
                    !attributes.TryGetProperty("POSITION", out var positionAccessor)) continue;
                var positions = ReadVectors(positionAccessor.GetInt32());
                if (positions.Length == 0) continue;
                var normals = attributes.TryGetProperty("NORMAL", out var normalAccessor) ? ReadVectors(normalAccessor.GetInt32()) : [];
                var indices = primitive.TryGetProperty("indices", out var indexAccessor)
                    ? ReadIndices(indexAccessor.GetInt32())
                    : Enumerable.Range(0, positions.Length).ToArray();
                var material = Material(primitive.TryGetProperty("material", out var materialIndex) ? materialIndex.GetInt32() : -1, mesh);
                var triangles = mode == 4 ? indices : Expand(indices, mode);
                if (mesh.TriangleCount + triangles.Length / 3 > options.MaximumTriangles)
                    throw new InvalidDataException("The model file is too large to import.");

                var start = mesh.Positions.Count;
                var mirrored = world.GetDeterminant() < 0;
                for (var i = 0; i < positions.Length; i++)
                {
                    mesh.Positions.Add(Vector3.Transform(positions[i], world));
                    if (i < normals.Length)
                    {
                        var normal = Vector3.TransformNormal(normals[i], world);
                        mesh.Normals!.Add(normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.Zero);
                    }
                    else mesh.Normals!.Add(Vector3.Zero);
                }
                for (var i = 0; i + 2 < triangles.Length; i += 3)
                {
                    var a = triangles[i];
                    var b = triangles[mirrored ? i + 2 : i + 1];
                    var c = triangles[mirrored ? i + 1 : i + 2];
                    if ((uint)a < (uint)positions.Length && (uint)b < (uint)positions.Length && (uint)c < (uint)positions.Length)
                        mesh.AddTriangle(start + a, start + b, start + c, material);
                }
            }
        }

        /// <summary>Converts triangle strips (5) and fans (6) to a plain triangle list.</summary>
        private static int[] Expand(int[] indices, int mode)
        {
            var list = new List<int>();
            for (var i = 2; i < indices.Length; i++)
            {
                if (mode == 5) list.AddRange((i & 1) == 0 ? [indices[i - 2], indices[i - 1], indices[i]] : [indices[i - 1], indices[i - 2], indices[i]]);
                else list.AddRange([indices[0], indices[i - 1], indices[i]]);
            }
            return [.. list];
        }

        private int Material(int index, MeshAccumulator mesh)
        {
            if (_materials.TryGetValue(index, out var cached)) return cached;
            var resolved = mesh.MaterialIndex($"gltf:{index}", () => BuildMaterial(index));
            return _materials[index] = resolved;
        }

        private RenderMaterial BuildMaterial(int index)
        {
            var materials = Array("materials");
            if ((uint)index >= (uint)materials.Count) return MaterialLibrary.Imported;
            var m = materials[index];
            var color = Vector3.One;
            var alpha = 1f;
            var metallic = 1f;
            var roughness = 1f;
            if (m.TryGetProperty("pbrMetallicRoughness", out var pbr))
            {
                if (pbr.TryGetProperty("baseColorFactor", out var factor) && factor.GetArrayLength() >= 4)
                {
                    var f = factor.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                    color = new Vector3(f[0], f[1], f[2]);
                    alpha = f[3];
                }
                if (pbr.TryGetProperty("metallicFactor", out var mf)) metallic = mf.GetSingle();
                if (pbr.TryGetProperty("roughnessFactor", out var rf)) roughness = rf.GetSingle();
            }
            var emission = m.TryGetProperty("emissiveFactor", out var e) && e.GetArrayLength() >= 3 ? ToVector(e) : Vector3.Zero;
            var blend = m.TryGetProperty("alphaMode", out var mode) && mode.GetString() == "BLEND" && alpha < 0.98f;
            var name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "glTF" : "glTF";
            // glTF base colors are already linear.
            return blend
                ? MaterialLibrary.ClearGlass with
                {
                    Name = name, Transmission = Math.Clamp(1 - alpha, 0.2f, 0.97f),
                    TransmissionTint = Vector3.Lerp(new Vector3(0.9f, 0.95f, 0.97f), color, 0.3f)
                }
                : new RenderMaterial { Name = name, BaseColor = color, Metallic = metallic < 0.5f ? 0 : metallic, Roughness = roughness, Emission = emission };
        }

        // ---- accessors

        private byte[] Buffer(int index)
        {
            if (_buffers.TryGetValue(index, out var cached)) return cached;
            var buffers = Array("buffers");
            if ((uint)index >= (uint)buffers.Count) throw new InvalidDataException("The glTF file is not valid.");
            var buffer = buffers[index];
            byte[] data;
            if (!buffer.TryGetProperty("uri", out var uri)) data = embedded ?? throw new InvalidDataException("The glTF file is not valid.");
            else
            {
                var text = uri.GetString() ?? "";
                if (text.StartsWith("data:", StringComparison.Ordinal))
                    data = Convert.FromBase64String(text[(text.IndexOf(',') + 1)..]);
                else
                {
                    var file = Path.GetFullPath(Path.Combine(directory, Uri.UnescapeDataString(text)));
                    // Buffers must stay beside the model; a path that climbs out of its folder is ignored.
                    if (!file.StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                        throw new InvalidDataException("The glTF file is not valid.");
                    data = File.ReadAllBytes(file);
                }
            }
            return _buffers[index] = data;
        }

        private (byte[] Data, int Offset, int Stride, int Count, int ComponentType, int Components) Accessor(int index)
        {
            var accessors = Array("accessors");
            if ((uint)index >= (uint)accessors.Count) throw new InvalidDataException("The glTF file is not valid.");
            var a = accessors[index];
            var count = a.GetProperty("count").GetInt32();
            var componentType = a.GetProperty("componentType").GetInt32();
            var components = a.GetProperty("type").GetString() switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => 1 };
            var accessorOffset = a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;
            if (!a.TryGetProperty("bufferView", out var viewIndex)) return ([], 0, 0, count, componentType, components);
            var views = Array("bufferViews");
            var view = views[viewIndex.GetInt32()];
            var data = Buffer(view.GetProperty("buffer").GetInt32());
            var offset = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) + accessorOffset;
            var size = componentType switch { 5126 or 5125 => 4, 5123 or 5122 => 2, _ => 1 };
            var stride = view.TryGetProperty("byteStride", out var bs) && bs.GetInt32() > 0 ? bs.GetInt32() : size * components;
            return (data, offset, stride, count, componentType, components);
        }

        private Vector3[] ReadVectors(int accessorIndex)
        {
            var (data, offset, stride, count, type, components) = Accessor(accessorIndex);
            if (data.Length == 0 || type != 5126 || components < 3) return [];
            var result = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var at = offset + i * stride;
                if (at + 12 > data.Length) break;
                result[i] = new Vector3(BitConverter.ToSingle(data, at), BitConverter.ToSingle(data, at + 4), BitConverter.ToSingle(data, at + 8));
            }
            return result;
        }

        private int[] ReadIndices(int accessorIndex)
        {
            var (data, offset, stride, count, type, _) = Accessor(accessorIndex);
            var result = new int[count];
            for (var i = 0; i < count; i++)
            {
                var at = offset + i * stride;
                result[i] = type switch
                {
                    5125 when at + 4 <= data.Length => (int)BitConverter.ToUInt32(data, at),
                    5123 when at + 2 <= data.Length => BitConverter.ToUInt16(data, at),
                    5121 when at < data.Length => data[at],
                    _ => 0
                };
            }
            return result;
        }
    }
}
