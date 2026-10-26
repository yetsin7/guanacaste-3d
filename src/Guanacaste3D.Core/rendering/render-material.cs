using System.Numerics;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core.Rendering;

/// <summary>Procedural surface detail evaluated from world position, so imported meshes need no texture coordinates.</summary>
public enum SurfacePattern : byte
{
    None,
    /// <summary>Two-tone squares one meter wide, used for the ground so scale reads on screen.</summary>
    Checker,
    /// <summary>Mottled green-brown breakup for lawns and terrain.</summary>
    Grass,
    /// <summary>Ceramic tiles with darker joints.</summary>
    Tiles,
    /// <summary>Long boards with slight tone variation.</summary>
    Planks,
    /// <summary>Masonry blocks in running bond.</summary>
    Blocks,
    /// <summary>Fine mottling for poured concrete and plaster.</summary>
    Concrete,
    /// <summary>Clusters of leaves: light and dark clumps with a strong, lumpy relief.</summary>
    Foliage,
    /// <summary>Furrowed bark running up a trunk.</summary>
    Bark
}

/// <summary>
/// A physically based surface description shared by the BIM scene and imported models.
/// Colors are linear RGB in 0–1; <see cref="Emission"/> is radiance and may exceed 1.
/// </summary>
public sealed record RenderMaterial
{
    public string Name { get; init; } = "Material";
    /// <summary>Diffuse reflectance for dielectrics, specular color for metals.</summary>
    public Vector3 BaseColor { get; init; } = new(0.7f);
    /// <summary>Microfacet roughness: 0 is a mirror, 1 fully matte.</summary>
    public float Roughness { get; init; } = 0.8f;
    public float Metallic { get; init; }
    /// <summary>Index of refraction; sets the reflectance at normal incidence of non-metals.</summary>
    public float Ior { get; init; } = 1.5f;
    /// <summary>Light emitted by the surface; non-zero makes it a light source.</summary>
    public Vector3 Emission { get; init; }
    /// <summary>
    /// 0 for opaque surfaces, 1 for clear thin glass. Fractions behave as the share of rays that pass,
    /// which matches an alpha-tested or tinted film.
    /// </summary>
    public float Transmission { get; init; }
    /// <summary>Color multiplied into light that passes through a transmissive surface.</summary>
    public Vector3 TransmissionTint { get; init; } = new(0.86f, 0.92f, 0.94f);
    public SurfacePattern Pattern { get; init; }
    /// <summary>Pattern size multiplier; 1 matches real-world dimensions in meters.</summary>
    public float PatternScale { get; init; } = 1;

    public bool IsEmissive => Emission.X > 0 || Emission.Y > 0 || Emission.Z > 0;
    public bool IsTransmissive => Transmission > 0.001f;

    /// <summary>Reflectance at normal incidence for a dielectric of this index of refraction.</summary>
    public float DielectricF0
    {
        get
        {
            var ratio = (Ior - 1) / (Ior + 1);
            return ratio * ratio;
        }
    }

    /// <summary>Returns the same material with every value forced into a range the integrator can trust.</summary>
    public RenderMaterial Sanitized() => this with
    {
        BaseColor = Clean(BaseColor, 0, 1, 0.7f),
        Roughness = Clean(Roughness, 0.02f, 1, 0.8f),
        Metallic = Clean(Metallic, 0, 1, 0),
        Ior = Clean(Ior, 1.0f, 3.0f, 1.5f),
        Transmission = Clean(Transmission, 0, 1, 0),
        Emission = Clean(Emission, 0, 10000, 0),
        TransmissionTint = Clean(TransmissionTint, 0, 1, 1),
        PatternScale = Clean(PatternScale, 0.01f, 1000, 1)
    };

    private static float Clean(float value, float low, float high, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, low, high) : fallback;

    private static Vector3 Clean(Vector3 value, float low, float high, float fallback) =>
        new(Clean(value.X, low, high, fallback), Clean(value.Y, low, high, fallback), Clean(value.Z, low, high, fallback));
}

/// <summary>Default materials for the BIM surface categories, plus common presets for imported geometry.</summary>
public static class MaterialLibrary
{
    /// <summary>Returns the stock material that renders a BIM surface category.</summary>
    public static RenderMaterial ForKind(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Wall => new RenderMaterial
        {
            Name = "Pared", BaseColor = new(0.80f, 0.78f, 0.73f), Roughness = 0.92f,
            Pattern = SurfacePattern.Concrete, PatternScale = 1.0f
        },
        SurfaceKind.Floor => new RenderMaterial
        {
            Name = "Piso", BaseColor = new(0.56f, 0.55f, 0.52f), Roughness = 0.38f,
            Pattern = SurfacePattern.Tiles, PatternScale = 0.6f
        },
        SurfaceKind.Glass => new RenderMaterial
        {
            Name = "Vidrio", BaseColor = Vector3.One, Roughness = 0.02f, Transmission = 1,
            Ior = 1.5f, TransmissionTint = new(0.86f, 0.92f, 0.94f)
        },
        SurfaceKind.Ground => new RenderMaterial
        {
            Name = "Terreno", BaseColor = new(0.22f, 0.28f, 0.13f), Roughness = 0.97f,
            Pattern = SurfacePattern.Grass, PatternScale = 1
        },
        SurfaceKind.DoorLeaf => new RenderMaterial
        {
            Name = "Madera", BaseColor = new(0.60f, 0.39f, 0.22f), Roughness = 0.5f,
            Pattern = SurfacePattern.Planks, PatternScale = 0.4f
        },
        SurfaceKind.OpeningFrame => new RenderMaterial
        {
            Name = "Marco", BaseColor = new(0.25f, 0.27f, 0.29f), Roughness = 0.45f, Metallic = 0.35f
        },
        SurfaceKind.Foliage => new RenderMaterial
        {
            Name = "Follaje", BaseColor = new(0.085f, 0.17f, 0.045f), Roughness = 0.62f,
            Pattern = SurfacePattern.Foliage, PatternScale = 1
        },
        SurfaceKind.Bark => new RenderMaterial
        {
            Name = "Corteza", BaseColor = new(0.20f, 0.16f, 0.12f), Roughness = 0.95f,
            Pattern = SurfacePattern.Bark, PatternScale = 1
        },
        SurfaceKind.Structure => new RenderMaterial
        {
            Name = "Estructura", BaseColor = new(0.45f, 0.42f, 0.38f), Roughness = 0.85f,
            Pattern = SurfacePattern.Concrete, PatternScale = 0.7f
        },
        _ => new RenderMaterial { Name = "Material", BaseColor = new(0.7f), Roughness = 0.8f }
    };

    /// <summary>Neutral material used for imported geometry that carries no material of its own.</summary>
    public static RenderMaterial Imported { get; } =
        new() { Name = "Importado", BaseColor = new(0.72f, 0.72f, 0.70f), Roughness = 0.75f };

    /// <summary>Clear-glass material used when an import marks a surface as transparent.</summary>
    public static RenderMaterial ClearGlass { get; } = ForKind(SurfaceKind.Glass);

    /// <summary>Converts an 8-bit sRGB color (0–255) to the linear value the renderer uses.</summary>
    public static Vector3 LinearFromSrgb(byte red, byte green, byte blue) =>
        new(SrgbToLinear(red / 255f), SrgbToLinear(green / 255f), SrgbToLinear(blue / 255f));

    /// <summary>Converts one 0–1 sRGB channel to linear light.</summary>
    public static float SrgbToLinear(float value) =>
        value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}
