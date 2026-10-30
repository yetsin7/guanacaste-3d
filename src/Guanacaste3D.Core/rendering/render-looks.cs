using System.Numerics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// A picture style: a starting point for the image effects, like the photo styles of visualisation packages. It is
/// applied on top of the scenario's own finish; the sliders then refine it.
/// </summary>
public sealed record ImageStyle(string Id, string NameKey, Func<ImageFinish, ImageFinish> Apply);

/// <summary>A sky: the light scenario, the hour and season, the clouds and the air that suit one kind of picture.</summary>
public sealed record SkyPreset(string Id, string NameKey, LightingScenario Scenario, int DayOfYear, double SolarHour,
    double CloudCover, CloudStyle Clouds, bool Haze, string StyleId);

/// <summary>The catalog of picture styles and skies offered by the Render panel.</summary>
public static class RenderLooks
{
    private static Vector3 Rgb(float red, float green, float blue) => new(red, green, blue);

    /// <summary>Picture styles, from neutral to strongly graded.</summary>
    public static IReadOnlyList<ImageStyle> Styles { get; } =
    [
        new("none", "RenderStyle.None", finish => finish),
        // Clean daylight architecture: open shadows, crisp detail, a touch of glare on glass.
        new("realistic", "RenderStyle.Realistic", finish => finish with
        {
            Shadows = 0.25f, Highlights = -0.2f, Vibrance = 0.25f, Sharpen = 0.35f, Glare = 0.15f, Bloom = 0.08f
        }),
        new("sunny", "RenderStyle.Sunny", finish => finish with
        {
            Temperature = 0.12f, Vibrance = 0.35f, Shadows = 0.15f, Contrast = 1.08f, Glare = 0.25f, Sharpen = 0.3f
        }),
        // Warm interiors: lifted shadows, golden highlights and soft glow from the lamps.
        new("interior", "RenderStyle.Interior", finish => finish with
        {
            Temperature = 0.3f, Shadows = 0.45f, Highlights = -0.25f, Bloom = 0.22f, HighlightTint = Rgb(1.0f, 0.95f, 0.85f),
            Vignette = 0.18f, Sharpen = 0.2f
        }),
        // Teal shadows and amber highlights, the look of feature films.
        new("cinematic", "RenderStyle.Cinematic", finish => finish with
        {
            ToneMapper = ToneMapper.Aces, Contrast = 1.12f, ShadowTint = Rgb(0.82f, 0.95f, 1.0f), HighlightTint = Rgb(1.0f, 0.93f, 0.82f),
            Vignette = 0.25f, FilmGrain = 0.25f, ChromaticAberration = 0.2f, Saturation = 0.98f
        }),
        new("golden", "RenderStyle.Golden", finish => finish with
        {
            Temperature = 0.55f, HighlightTint = Rgb(1.0f, 0.88f, 0.72f), Shadows = 0.2f, Bloom = 0.25f, Glare = 0.35f, Vibrance = 0.2f
        }),
        new("blue-hour", "RenderStyle.BlueHour", finish => finish with
        {
            Temperature = -0.35f, ShadowTint = Rgb(0.80f, 0.88f, 1.0f), HighlightTint = Rgb(1.0f, 0.90f, 0.78f), Bloom = 0.3f,
            Glare = 0.3f, Contrast = 1.06f
        }),
        // Overcast editorial: low contrast, even light, quiet colour.
        new("soft", "RenderStyle.Soft", finish => finish with
        {
            ToneMapper = ToneMapper.Soft, Contrast = 0.94f, Shadows = 0.35f, Highlights = -0.3f, Saturation = 0.95f, Vibrance = 0.1f
        }),
        new("dramatic", "RenderStyle.Dramatic", finish => finish with
        {
            ToneMapper = ToneMapper.Aces, Contrast = 1.22f, Highlights = -0.35f, Shadows = -0.1f, Vignette = 0.32f, Sharpen = 0.45f,
            Saturation = 0.92f, ShadowTint = Rgb(0.88f, 0.92f, 1.0f)
        }),
        new("misty", "RenderStyle.Misty", finish => finish with
        {
            Fog = 0.45f, Contrast = 0.95f, Shadows = 0.3f, Saturation = 0.9f, FogColor = Rgb(0.82f, 0.86f, 0.90f)
        }),
        new("editorial", "RenderStyle.Editorial", finish => finish with
        {
            Temperature = 0.05f, Saturation = 0.9f, Contrast = 1.04f, Shadows = 0.3f, HighlightTint = Rgb(1.0f, 0.97f, 0.93f),
            FilmGrain = 0.12f
        }),
        new("monochrome", "RenderStyle.Monochrome", finish => finish with
        {
            Monochrome = true, Contrast = 1.15f, Sharpen = 0.4f, FilmGrain = 0.3f, Vignette = 0.2f
        }),
        new("noir", "Cine negro", finish => finish with { Monochrome = true, Contrast = 1.35f, Vignette = .42f, FilmGrain = .35f }),
        new("pastel", "Pastel suave", finish => finish with { Saturation = .72f, Contrast = .88f, Shadows = .42f, Highlights = -.22f }),
        new("vintage", "Fotografía vintage", finish => finish with { Temperature = .34f, Saturation = .78f, FilmGrain = .4f, Vignette = .3f }),
        new("high-key", "Estudio luminoso", finish => finish with { Shadows = .58f, Highlights = -.18f, Contrast = .9f, Sharpen = .22f }),
        new("low-key", "Estudio dramático", finish => finish with { Shadows = -.3f, Contrast = 1.32f, Vignette = .4f }),
        new("night-architecture", "Arquitectura nocturna", finish => finish with { Temperature = -.28f, Bloom = .35f, Glare = .5f, Shadows = .24f }),
        new("warm-minimal", "Minimalista cálido", finish => finish with { Temperature = .24f, Saturation = .88f, Contrast = .96f, Sharpen = .2f }),
        new("cool-modern", "Moderno frío", finish => finish with { Temperature = -.24f, Contrast = 1.08f, Vibrance = .18f, Sharpen = .4f }),
        new("lush-landscape", "Paisaje vibrante", finish => finish with { Vibrance = .58f, Saturation = 1.1f, Shadows = .25f, Sharpen = .3f }),
        new("soft-dream", "Ensueño", finish => finish with { Bloom = .42f, Glare = .25f, Contrast = .9f, Sharpen = .05f })
    ];

    /// <summary>Skies: each pairs a light with clouds and a fitting style.</summary>
    public static IReadOnlyList<SkyPreset> Skies { get; } =
    [
        new("clear-noon", "RenderSky.ClearNoon", LightingScenario.Sunny, 172, 12.0, 0.05, CloudStyle.Cumulus, true, "sunny"),
        new("morning", "RenderSky.Morning", LightingScenario.Sunny, 80, 8.5, 0.25, CloudStyle.Cumulus, true, "realistic"),
        new("summer-cumulus", "RenderSky.SummerCumulus", LightingScenario.Sunny, 200, 11.0, 0.45, CloudStyle.Cumulus, true, "realistic"),
        new("cirrus", "RenderSky.Cirrus", LightingScenario.Sunny, 355, 14.75, 0.55, CloudStyle.Cirrus, true, "realistic"),
        new("afternoon", "RenderSky.Afternoon", LightingScenario.Sunny, 300, 15.5, 0.3, CloudStyle.Cumulus, true, "sunny"),
        new("golden-hour", "RenderSky.GoldenHour", LightingScenario.GoldenHour, 172, 17.5, 0.35, CloudStyle.Cirrus, true, "golden"),
        new("blue-hour", "RenderSky.BlueHour", LightingScenario.Dusk, 172, 18.5, 0.25, CloudStyle.Cirrus, true, "blue-hour"),
        new("overcast", "RenderSky.Overcast", LightingScenario.Overcast, 100, 11.0, 1.0, CloudStyle.Stratocumulus, true, "soft"),
        new("broken", "RenderSky.Broken", LightingScenario.Sunny, 250, 13.0, 0.7, CloudStyle.Stratocumulus, true, "realistic"),
        new("storm", "RenderSky.Storm", LightingScenario.Sunny, 260, 16.0, 0.8, CloudStyle.Storm, true, "dramatic"),
        new("misty-morning", "RenderSky.MistyMorning", LightingScenario.Sunny, 320, 7.5, 0.4, CloudStyle.Stratocumulus, true, "misty")
    ];

    /// <summary>The style with an identifier, or the neutral one.</summary>
    public static ImageStyle Style(string? id) => Styles.FirstOrDefault(style => style.Id == id) ?? Styles[0];
}
