using System.Text.Json;
using System.Text.Json.Nodes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Guanacaste3D.RevitBridge;

/// <summary>
/// Writes a Revit document as a Guanacaste exchange file (.g3dx): JSON with the levels, the materials, the triangle
/// meshes and every model element with the BIM data Guanacaste needs to rebuild it natively (wall centerlines, hosted
/// door and window positions, slab outlines, rooms). Lengths are meters, Z up, in Revit's internal coordinates.
/// Identical family geometry is written once: an instance refers to a mesh in its own frame (origin plus a turn about Z).
/// The format is documented in docs/revit-import.md.
/// </summary>
internal sealed class ExchangeExporter
{
    public const int FormatVersion = 1;
    private const double Meters = 0.3048;

    private readonly Document _document;
    private readonly View3D? _view;
    private readonly Options _options = new() { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false };
    private readonly Dictionary<long, int> _materialIndex = [];
    private readonly List<JsonObject> _materials = [];
    private readonly Dictionary<ulong, int> _meshByHash = [];
    private readonly List<MeshBuilder> _meshes = [];
    private readonly JsonArray _elements = [];
    private readonly Dictionary<string, int> _skipped = new(StringComparer.Ordinal);

    /// <summary>Exports the whole model, or with <paramref name="view"/> only what that 3D view shows (hidden
    /// categories, filters, section box and phase applied).</summary>
    public ExchangeExporter(Document document, View3D? view = null)
    {
        _document = document;
        _view = view;
    }

    public int ElementCount => _elements.Count;
    public int TriangleCount => _meshes.Sum(mesh => mesh.TriangleCount);

    /// <summary>Reads the whole model and writes the exchange file, reporting progress as a fraction.</summary>
    public void Export(string path, Action<double>? progress = null)
    {
        var levels = new FilteredElementCollector(_document).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(level => level.ProjectElevation).ToList();
        var collector = _view is null ? new FilteredElementCollector(_document) : new FilteredElementCollector(_document, _view.Id);
        var candidates = collector.WhereElementIsNotElementType()
            .Where(IsModelElement).ToList();
        for (var index = 0; index < candidates.Count; index++)
        {
            try
            {
                Visit(candidates[index]);
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ApplicationException or InvalidOperationException
                                           or ArgumentException or NullReferenceException)
            {
                Count("error:" + ex.GetType().Name);
            }
            if (index % 50 == 0) progress?.Invoke((double)index / Math.Max(1, candidates.Count));
        }
        foreach (var room in new FilteredElementCollector(_document).OfCategory(BuiltInCategory.OST_Rooms)
                     .WhereElementIsNotElementType().Cast<SpatialElement>())
            VisitRoom(room);
        Write(path, levels);
    }

    /// <summary>Elements with model geometry, leaving out datums, annotation, imported CAD, demolished and secondary options.</summary>
    private bool IsModelElement(Element element)
    {
        if (element.Category is not { CategoryType: CategoryType.Model } category) return false;
        if (element is ImportInstance or RevitLinkInstance or SpatialElement) return false;
        if (element.ViewSpecific) return false;
        var builtIn = (BuiltInCategory)category.Id.Value;
        if (builtIn is BuiltInCategory.OST_Lines or BuiltInCategory.OST_Cameras or BuiltInCategory.OST_Mass
            or BuiltInCategory.OST_SectionBox or BuiltInCategory.OST_Levels or BuiltInCategory.OST_Grids
            or BuiltInCategory.OST_Rooms or BuiltInCategory.OST_Areas or BuiltInCategory.OST_RvtLinks
            or BuiltInCategory.OST_LightingFixtureSource or BuiltInCategory.OST_MEPSpaces
            or BuiltInCategory.OST_StairsRuns or BuiltInCategory.OST_StairsLandings
            or BuiltInCategory.OST_StairsSupports)
            return false;
        if (element.DesignOption is { IsPrimary: false }) return false;
        if (element.DemolishedPhaseId is { } demolished && demolished != ElementId.InvalidElementId) return false;
        return true;
    }

    private void Visit(Element element)
    {
        var record = new JsonObject
        {
            ["id"] = element.Id.Value,
            ["uniqueId"] = element.UniqueId,
            ["category"] = element.Category is { } category ? ((BuiltInCategory)category.Id.Value).ToString() : "",
            ["categoryName"] = element.Category?.Name ?? "",
            ["name"] = element.Name ?? ""
        };
        var type = _document.GetElement(element.GetTypeId()) as ElementType;
        record["family"] = type?.FamilyName ?? "";
        record["type"] = type?.Name ?? "";
        if (LevelOf(element) is { } level) record["level"] = level.Value;
        if (element.LookupParameter("Mark") is { HasValue: true } mark) record["mark"] = mark.AsString() ?? "";

        // The instance frame lets identical family geometry be shared; everything else is written in model coordinates.
        var (origin, rotation) = FrameOf(element);
        var builder = new MeshBuilder(origin, rotation);
        if (element.get_Geometry(_options) is { } geometry) Collect(geometry, builder, element);
        if (builder.TriangleCount == 0 && element is not Wall)
        {
            Count("empty:" + record["category"]);
            return;
        }
        if (builder.TriangleCount > 0)
        {
            record["mesh"] = Share(builder);
            record["origin"] = new JsonArray(origin.X * Meters, origin.Y * Meters, origin.Z * Meters);
            record["rotation"] = rotation;
        }

        switch (element)
        {
            case Wall wall:
                record["kind"] = "wall";
                record["wall"] = DescribeWall(wall);
                break;
            case FamilyInstance { Host: Wall host } instance
                when instance.Category?.Id.Value is (long)BuiltInCategory.OST_Doors or (long)BuiltInCategory.OST_Windows:
                record["kind"] = instance.Category.Id.Value == (long)BuiltInCategory.OST_Doors ? "door" : "window";
                record["opening"] = DescribeOpening(instance, host);
                break;
            case Floor or Ceiling or RoofBase:
                record["kind"] = element switch { Floor => "floor", Ceiling => "ceiling", _ => "roof" };
                if (DescribeSlab((HostObject)element) is { } slab) record["slab"] = slab;
                break;
            case FamilyInstance:
                record["kind"] = "instance";
                break;
            default:
                record["kind"] = "mesh";
                break;
        }
        _elements.Add(record);
    }

    /// <summary>Insertion point and turn about Z of an upright family instance; the model origin for everything else.</summary>
    private static (XYZ Origin, double Rotation) FrameOf(Element element)
    {
        if (element is not FamilyInstance instance) return (XYZ.Zero, 0);
        var transform = instance.GetTotalTransform();
        var axis = new XYZ(transform.BasisX.X, transform.BasisX.Y, 0);
        if (Math.Abs(transform.BasisZ.Z) < 0.999 || axis.GetLength() < 1e-6) return (transform.Origin, 0);
        return (transform.Origin, Math.Atan2(axis.Y, axis.X));
    }

    /// <summary>Adds the triangles of solids, meshes and nested instances, in model coordinates.</summary>
    private void Collect(GeometryElement geometry, MeshBuilder builder, Element owner)
    {
        foreach (var item in geometry)
        {
            switch (item)
            {
                case Solid { Faces.Size: > 0 } solid:
                    foreach (Face face in solid.Faces)
                    {
                        Mesh mesh;
                        try { mesh = face.Triangulate(0.35); }
                        catch (Autodesk.Revit.Exceptions.ApplicationException) { continue; }
                        if (mesh is null) continue;
                        AddMesh(mesh, builder, MaterialOf(face.MaterialElementId, owner));
                    }
                    break;
                case Mesh mesh:
                    AddMesh(mesh, builder, MaterialOf(mesh.MaterialElementId, owner));
                    break;
                case GeometryInstance instance when instance.GetInstanceGeometry() is { } nested:
                    Collect(nested, builder, owner);
                    break;
            }
        }
    }

    private static void AddMesh(Mesh mesh, MeshBuilder builder, int material)
    {
        var start = builder.VertexCount;
        foreach (XYZ vertex in mesh.Vertices) builder.AddVertex(vertex);
        for (var index = 0; index < mesh.NumTriangles; index++)
        {
            var triangle = mesh.get_Triangle(index);
            builder.AddTriangle(start + (int)triangle.get_Index(0), start + (int)triangle.get_Index(1),
                start + (int)triangle.get_Index(2), material);
        }
    }

    /// <summary>Index of a face's material, falling back to the element's category material.</summary>
    private int MaterialOf(ElementId id, Element owner)
    {
        if (id == ElementId.InvalidElementId && owner.Category?.Material is { } categoryMaterial) id = categoryMaterial.Id;
        if (_materialIndex.TryGetValue(id.Value, out var index)) return index;
        var material = _document.GetElement(id) as Material;
        var color = material?.Color is { IsValid: true } valid ? valid : null;
        var entry = new JsonObject
        {
            ["name"] = material?.Name ?? owner.Category?.Name ?? "Default",
            ["class"] = material?.MaterialClass ?? "",
            ["category"] = material?.MaterialCategory ?? "",
            ["color"] = color is null ? "#B4B4B4" : $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}",
            ["transparency"] = (material?.Transparency ?? 0) / 100.0,
            ["shininess"] = (material?.Shininess ?? 0) / 128.0,
            ["ownerCategory"] = owner.Category is { } category ? ((BuiltInCategory)category.Id.Value).ToString() : ""
        };
        index = _materials.Count;
        _materials.Add(entry);
        _materialIndex[id.Value] = index;
        return index;
    }

    /// <summary>Stores a mesh once per distinct geometry and returns its index.</summary>
    private int Share(MeshBuilder builder)
    {
        var hash = builder.Hash();
        if (_meshByHash.TryGetValue(hash, out var existing)) return existing;
        _meshes.Add(builder);
        return _meshByHash[hash] = _meshes.Count - 1;
    }

    private JsonObject DescribeWall(Wall wall)
    {
        var data = new JsonObject
        {
            ["wallKind"] = wall.WallType.Kind.ToString(),
            ["width"] = wall.Width * Meters,
            ["function"] = wall.WallType.Function.ToString()
        };
        if (wall.Location is LocationCurve { Curve: { } curve })
        {
            // Revit places the wall on its location line (a face or the core); Guanacaste walls run on the centerline.
            var structure = wall.WallType.GetCompoundStructure();
            var key = (WallLocationLine)(wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM)?.AsInteger() ?? 0);
            var offset = structure?.GetOffsetForLocationLine(key) ?? 0;
            var shift = wall.Orientation.Multiply(-offset);
            var start = curve.GetEndPoint(0) + shift;
            var end = curve.GetEndPoint(1) + shift;
            data["start"] = new JsonArray(start.X * Meters, start.Y * Meters);
            data["end"] = new JsonArray(end.X * Meters, end.Y * Meters);
            data["curved"] = curve is not Line;
            data["locationLine"] = key.ToString();
            data["locationOffset"] = offset * Meters;
            data["orientation"] = new JsonArray(wall.Orientation.X, wall.Orientation.Y);
            if (curve is Arc arc)
            {
                data["center"] = new JsonArray((arc.Center.X + shift.X) * Meters, (arc.Center.Y + shift.Y) * Meters);
                data["radius"] = arc.Radius * Meters;
            }
        }
        var baseLevel = _document.GetElement(wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId()
                                             ?? ElementId.InvalidElementId) as Level;
        var baseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0;
        if (baseLevel is not null)
        {
            data["baseLevel"] = baseLevel.Id.Value;
            data["baseZ"] = (baseLevel.ProjectElevation + baseOffset) * Meters;
        }
        data["height"] = (wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0) * Meters;
        if (wall.get_BoundingBox(null) is { } box)
        {
            data["bottomZ"] = box.Min.Z * Meters;
            data["topZ"] = box.Max.Z * Meters;
        }
        var topLevel = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.AsElementId();
        if (topLevel is not null && topLevel != ElementId.InvalidElementId) data["topLevel"] = topLevel.Value;
        data["topOffset"] = (wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble() ?? 0) * Meters;
        data["profileEdited"] = wall.SketchId != ElementId.InvalidElementId;
        data["topAttached"] = wall.get_Parameter(BuiltInParameter.WALL_TOP_IS_ATTACHED)?.AsInteger() == 1;
        data["baseAttached"] = wall.get_Parameter(BuiltInParameter.WALL_BOTTOM_IS_ATTACHED)?.AsInteger() == 1;
        var layers = new JsonArray();
        if (wall.WallType.GetCompoundStructure() is { } compound)
            foreach (var layer in compound.GetLayers())
            {
                var material = _document.GetElement(layer.MaterialId) as Material;
                layers.Add(new JsonObject
                {
                    ["function"] = layer.Function.ToString(),
                    ["width"] = layer.Width * Meters,
                    ["material"] = material?.Name ?? "",
                    ["class"] = material?.MaterialClass ?? ""
                });
            }
        data["layers"] = layers;
        return data;
    }

    private JsonObject DescribeOpening(FamilyInstance instance, Wall host)
    {
        var point = (instance.Location as LocationPoint)?.Point ?? instance.GetTotalTransform().Origin;
        var door = instance.Category?.Id.Value == (long)BuiltInCategory.OST_Doors;
        var width = Length(instance, door ? BuiltInParameter.DOOR_WIDTH : BuiltInParameter.WINDOW_WIDTH,
            BuiltInParameter.FAMILY_WIDTH_PARAM, BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM);
        var height = Length(instance, door ? BuiltInParameter.DOOR_HEIGHT : BuiltInParameter.WINDOW_HEIGHT,
            BuiltInParameter.FAMILY_HEIGHT_PARAM, BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM);
        var roughWidth = Length(instance, BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM);
        var roughHeight = Length(instance, BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM);
        var sill = instance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.AsDouble();
        var levelZ = _document.GetElement(instance.LevelId) is Level level ? level.ProjectElevation : (double?)null;
        var data = new JsonObject
        {
            ["host"] = host.Id.Value,
            ["point"] = new JsonArray(point.X * Meters, point.Y * Meters, point.Z * Meters),
            ["facing"] = new JsonArray(instance.FacingOrientation.X, instance.FacingOrientation.Y),
            ["hand"] = new JsonArray(instance.HandOrientation.X, instance.HandOrientation.Y),
            ["facingFlipped"] = instance.FacingFlipped,
            ["handFlipped"] = instance.HandFlipped,
            ["mirrored"] = instance.Mirrored
        };
        if (width is { } w) data["width"] = w * Meters;
        if (height is { } h) data["height"] = h * Meters;
        if (roughWidth is { } rw) data["roughWidth"] = rw * Meters;
        if (roughHeight is { } rh) data["roughHeight"] = rh * Meters;
        if (sill is { } s && levelZ is { } z) data["sillZ"] = (z + s) * Meters;
        return data;
    }

    /// <summary>The first length found on the instance, then on its type, among the given parameters.</summary>
    private static double? Length(FamilyInstance instance, params BuiltInParameter[] parameters)
    {
        foreach (var parameter in parameters)
        {
            foreach (var owner in new Element[] { instance, instance.Symbol })
                if (owner.get_Parameter(parameter) is { HasValue: true, StorageType: StorageType.Double } value &&
                    value.AsDouble() > 1e-6)
                    return value.AsDouble();
        }
        return null;
    }

    /// <summary>Outline loops, top elevation and thickness of a floor, ceiling or roof; flat when it has one level top.</summary>
    private JsonObject? DescribeSlab(HostObject host)
    {
        var references = host is Ceiling ? HostObjectUtils.GetBottomFaces(host) : HostObjectUtils.GetTopFaces(host);
        var faces = references.Select(reference => host.GetGeometryObjectFromReference(reference)).OfType<Face>().ToList();
        if (faces.Count == 0) return null;
        var flat = faces.Count == 1 && faces[0] is PlanarFace { FaceNormal.Z: var nz } && Math.Abs(nz) > 0.9995;
        var data = new JsonObject { ["flat"] = flat };
        if (faces[0] is PlanarFace planar)
        {
            var loops = new JsonArray();
            foreach (var loop in planar.GetEdgesAsCurveLoops())
            {
                var points = new JsonArray();
                foreach (var curve in loop)
                {
                    var tessellated = curve.Tessellate();
                    for (var index = 0; index < tessellated.Count - 1; index++)
                        points.Add(new JsonArray(tessellated[index].X * Meters, tessellated[index].Y * Meters));
                }
                loops.Add(points);
            }
            data["loops"] = loops;
            data["z"] = planar.Origin.Z * Meters;
        }
        var thickness = host switch
        {
            Floor => host.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble(),
            RoofBase => host.get_Parameter(BuiltInParameter.ROOF_ATTR_THICKNESS_PARAM)?.AsDouble(),
            _ => host.get_Parameter(BuiltInParameter.CEILING_THICKNESS)?.AsDouble()
        };
        if (thickness is > 0) data["thickness"] = thickness.Value * Meters;
        if (host is RoofBase) data["roofClass"] = host.GetType().Name;
        return data;
    }

    private void VisitRoom(SpatialElement room)
    {
        if (room.Area <= 0) return;
        var loops = new JsonArray();
        foreach (var segments in room.GetBoundarySegments(new SpatialElementBoundaryOptions()))
        {
            var points = new JsonArray();
            foreach (var segment in segments)
            {
                var tessellated = segment.GetCurve().Tessellate();
                for (var index = 0; index < tessellated.Count - 1; index++)
                    points.Add(new JsonArray(tessellated[index].X * Meters, tessellated[index].Y * Meters));
            }
            loops.Add(points);
        }
        _elements.Add(new JsonObject
        {
            ["id"] = room.Id.Value,
            ["uniqueId"] = room.UniqueId,
            ["category"] = nameof(BuiltInCategory.OST_Rooms),
            ["categoryName"] = room.Category?.Name ?? "",
            ["kind"] = "room",
            ["name"] = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? room.Name,
            ["number"] = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "",
            ["level"] = room.LevelId.Value,
            ["area"] = room.Area * Meters * Meters,
            ["loops"] = loops
        });
    }

    /// <summary>The level an element belongs to: its own, its host's, or its schedule level.</summary>
    private static ElementId? LevelOf(Element element)
    {
        if (element.LevelId != ElementId.InvalidElementId) return element.LevelId;
        foreach (var parameter in new[] { BuiltInParameter.FAMILY_LEVEL_PARAM, BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                     BuiltInParameter.WALL_BASE_CONSTRAINT, BuiltInParameter.STAIRS_BASE_LEVEL_PARAM })
            if (element.get_Parameter(parameter)?.AsElementId() is { } id && id != ElementId.InvalidElementId) return id;
        return element is FamilyInstance { Host: { } host } ? LevelOf(host) : null;
    }

    private void Count(string key) => _skipped[key] = _skipped.GetValueOrDefault(key) + 1;

    private void Write(string path, IReadOnlyList<Level> levels)
    {
        var temporary = path + ".partial";
        using (var stream = File.Create(temporary))
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("format", "guanacaste-exchange");
            writer.WriteNumber("version", FormatVersion);
            writer.WriteString("source", "Revit " + _document.Application.VersionNumber);
            writer.WriteString("sourcePath", _document.PathName);
            // The file name, not Project Information: templates leave sample names such as "Snowdon Towers" there.
            writer.WriteString("projectName", _document.Title);
            writer.WriteString("units", "m");
            writer.WritePropertyName("levels");
            writer.WriteStartArray();
            foreach (var level in levels)
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", level.Id.Value);
                writer.WriteString("name", level.Name);
                writer.WriteNumber("elevation", level.ProjectElevation * Meters);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("materials");
            new JsonArray([.. _materials.Select(material => (JsonNode)material)]).WriteTo(writer);
            writer.WritePropertyName("meshes");
            writer.WriteStartArray();
            foreach (var mesh in _meshes) mesh.WriteTo(writer);
            writer.WriteEndArray();
            writer.WritePropertyName("elements");
            _elements.WriteTo(writer);
            writer.WritePropertyName("skipped");
            writer.WriteStartObject();
            foreach (var (key, count) in _skipped) writer.WriteNumber(key, count);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        File.Move(temporary, path, overwrite: true);
    }
}
