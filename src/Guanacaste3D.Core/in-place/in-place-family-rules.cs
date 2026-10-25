using System.Numerics;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>
/// Validation and regeneration of in-place families. Geometry is never stored: each family is
/// tessellated from its operations, so a parameter or profile change regenerates the solid
/// without recreating the extrusion.
/// </summary>
public static class InPlaceFamilyRules
{
    public const int MaximumFamilies = 1000;
    public const int MaximumParameters = 200;
    public const int MaximumExtrusions = 500;

    /// <summary>Smallest accepted extrusion depth or length parameter, in meters (1 mm).</summary>
    public const double MinimumDepth = 0.001;

    /// <summary>Largest accepted extrusion depth or length parameter, in meters (1 km).</summary>
    public const double MaximumDepth = 1000;

    /// <summary>Largest accepted distance of an extrusion start from its work plane, in meters.</summary>
    public const double MaximumOffset = 1000;

    /// <summary>Largest deviation from unit length or orthogonality accepted for a work plane's axes.</summary>
    private const double PlaneTolerance = 1e-6;

    /// <summary>Rejects an invalid family, naming the first problem found, and proves each operation regenerates.</summary>
    public static void Validate(BuildingDocument document)
    {
        var families = document.InPlaceFamilies;
        if (families is null || families.Count > MaximumFamilies)
            throw new InvalidDataException("The in-place family collection is invalid.");
        foreach (var family in families)
        {
            if (family is null || family.Id == Guid.Empty || !IsValidName(family.Name, 120) ||
                family.CategoryId is null || FamilyCategoryCatalog.Find(family.CategoryId) is not { SupportsInPlace: true })
                throw new InvalidDataException(
                    "An in-place family is invalid or uses a category that does not accept in-place families.");
            ValidateParameters(family);
            ValidateExtrusions(family);
            Tessellate(family);
        }
        var identifiers = families.Select(family => family.Id)
            .Concat(families.SelectMany(family => family.Extrusions.Select(extrusion => extrusion.Id)))
            .ToArray();
        if (identifiers.Distinct().Count() != identifiers.Length ||
            families.Select(family => family.Name.Trim().ToUpperInvariant()).Distinct().Count() != families.Count)
            throw new InvalidDataException("In-place family and extrusion identifiers and names must be unique.");
    }

    /// <summary>
    /// Regeneration: a bound extrusion copies its parameter's value into <see cref="InPlaceExtrusion.Depth"/>,
    /// so stored data never disagrees with the model. A missing parameter is left for validation to reject.
    /// </summary>
    public static void Synchronize(BuildingDocument document)
    {
        foreach (var family in document.InPlaceFamilies)
        {
            for (var index = 0; index < family.Extrusions.Count; index++)
            {
                var extrusion = family.Extrusions[index];
                if (extrusion.DepthParameter is null) continue;
                var parameter = family.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, extrusion.DepthParameter, StringComparison.OrdinalIgnoreCase));
                if (parameter is not null && extrusion.Depth != parameter.Value)
                    family.Extrusions[index] = extrusion with { Depth = parameter.Value };
            }
        }
    }

    /// <summary>Returns the depth an extrusion uses: its bound parameter's value, or its stored depth.</summary>
    public static double EffectiveDepth(InPlaceFamily family, InPlaceExtrusion extrusion)
    {
        if (extrusion.DepthParameter is null) return extrusion.Depth;
        var parameter = family.Parameters.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, extrusion.DepthParameter, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The extrusion depth refers to a parameter that does not exist.");
        return parameter.Value;
    }

    /// <summary>Appends a family's regenerated solids to a scene mesh. The shared mesher uses this for every view and export.</summary>
    public static void AppendTo(SceneMesh mesh, InPlaceFamily family)
    {
        foreach (var extrusion in family.Extrusions)
            AppendExtrusion(mesh, extrusion, EffectiveDepth(family, extrusion));
    }

    /// <summary>Builds a standalone triangle mesh for one family by regenerating every extrusion in order.</summary>
    public static SceneMesh Tessellate(InPlaceFamily family)
    {
        var mesh = new SceneMesh();
        AppendTo(mesh, family);
        return mesh;
    }

    /// <summary>
    /// Returns the signed volume enclosed by a closed mesh in cubic meters. Outward-facing
    /// triangles give a positive result, so this also checks face orientation.
    /// </summary>
    public static double SignedVolume(SceneMesh mesh)
    {
        var sum = 0d;
        for (var index = 0; index < mesh.Indices.Count; index += 3)
        {
            sum += Determinant(
                mesh.Positions[mesh.Indices[index]],
                mesh.Positions[mesh.Indices[index + 1]],
                mesh.Positions[mesh.Indices[index + 2]]);
        }
        return sum / 6;
    }

    /// <summary>
    /// Emits the prism swept by a counter-clockwise profile: a bottom cap, a top cap and one
    /// quad per profile edge. Caps come from the ear-clipping triangulation of the same loop.
    /// </summary>
    private static void AppendExtrusion(SceneMesh mesh, InPlaceExtrusion extrusion, double depth)
    {
        var profile = InPlaceProfileRules.CounterClockwise(extrusion.Profile);
        IReadOnlyList<(int A, int B, int C)> triangles;
        try
        {
            triangles = PolygonTriangulator.Triangulate(profile);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"The profile cannot be triangulated: {exception.Message}");
        }
        var plane = extrusion.Plane;
        var bottom = profile.Select(point =>
            ToVector(plane.ToModel(point) + plane.Normal * extrusion.StartOffset)).ToArray();
        var top = profile.Select(point =>
            ToVector(plane.ToModel(point) + plane.Normal * (extrusion.StartOffset + depth))).ToArray();
        foreach (var (a, b, c) in triangles)
        {
            mesh.AddTriangle(top[a], top[b], top[c], SurfaceKind.Wall);
            mesh.AddTriangle(bottom[a], bottom[c], bottom[b], SurfaceKind.Wall);
        }
        for (var index = 0; index < profile.Count; index++)
        {
            var next = (index + 1) % profile.Count;
            mesh.AddQuad(bottom[index], bottom[next], top[next], top[index], SurfaceKind.Wall);
        }
    }

    /// <summary>Validates the plane's origin, unit axes and orthogonality.</summary>
    private static void ValidatePlane(WorkPlane plane)
    {
        if (!plane.Origin.IsFinite() || !plane.Normal.IsFinite() || !plane.XAxis.IsFinite() ||
            Math.Abs(plane.Normal.Norm() - 1) > PlaneTolerance ||
            Math.Abs(plane.XAxis.Norm() - 1) > PlaneTolerance ||
            Math.Abs(plane.Normal.Dot(plane.XAxis)) > PlaneTolerance)
            throw new InvalidDataException("The work plane must have a finite origin and orthogonal unit axes.");
    }

    /// <summary>Validates parameter names, values and uniqueness within one family.</summary>
    private static void ValidateParameters(InPlaceFamily family)
    {
        if (family.Parameters is null || family.Parameters.Count > MaximumParameters)
            throw new InvalidDataException($"Family '{family.Name}' has an invalid parameter list.");
        foreach (var parameter in family.Parameters)
        {
            if (parameter is null || !IsValidName(parameter.Name, 60) || !double.IsFinite(parameter.Value) ||
                parameter.Value < MinimumDepth || parameter.Value > MaximumDepth)
                throw new InvalidDataException($"A parameter of family '{family.Name}' is invalid.");
        }
        if (family.Parameters.Select(parameter => parameter.Name.ToUpperInvariant()).Distinct().Count() !=
            family.Parameters.Count)
            throw new InvalidDataException($"Parameter names in family '{family.Name}' must be unique.");
    }

    /// <summary>Validates each extrusion's plane, profile, offset and effective depth.</summary>
    private static void ValidateExtrusions(InPlaceFamily family)
    {
        if (family.Extrusions is null || family.Extrusions.Count > MaximumExtrusions)
            throw new InvalidDataException($"Family '{family.Name}' has an invalid extrusion list.");
        foreach (var extrusion in family.Extrusions)
        {
            if (extrusion is null || extrusion.Id == Guid.Empty || !IsValidName(extrusion.Name, 120) ||
                extrusion.Plane is null || extrusion.Profile is null || extrusion.MaterialName is null ||
                extrusion.MaterialName.Length > 120 || !double.IsFinite(extrusion.StartOffset) ||
                Math.Abs(extrusion.StartOffset) > MaximumOffset)
                throw new InvalidDataException($"An extrusion in family '{family.Name}' is invalid.");
            ValidatePlane(extrusion.Plane);
            InPlaceProfileRules.Validate(extrusion.Profile);
            var depth = EffectiveDepth(family, extrusion);
            if (!double.IsFinite(depth) || depth < MinimumDepth || depth > MaximumDepth)
                throw new InvalidDataException(
                    $"The depth of extrusion '{extrusion.Name}' must be between 1 mm and 1 km.");
        }
    }

    /// <summary>Returns whether a name is non-blank and within the length limit.</summary>
    private static bool IsValidName(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    /// <summary>Returns the determinant a · (b × c), computed in double precision.</summary>
    private static double Determinant(Vector3 a, Vector3 b, Vector3 c)
    {
        var (ax, ay, az) = ((double)a.X, (double)a.Y, (double)a.Z);
        var (bx, by, bz) = ((double)b.X, (double)b.Y, (double)b.Z);
        var (cx, cy, cz) = ((double)c.X, (double)c.Y, (double)c.Z);
        return ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
    }

    /// <summary>Converts a model point to the single-precision vertex format used by the mesh.</summary>
    private static Vector3 ToVector(Point3 point) => new((float)point.X, (float)point.Y, (float)point.Z);
}
