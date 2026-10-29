namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// The render view a project keeps: the camera, the sun and sky, the output size and quality, and the image filters
/// (look, exposure, white balance, bloom, vignette). Opening the project restores it, so a saved render can be
/// repeated exactly; saving the project stores the Render panel's current setup.
/// </summary>
public sealed record RenderSetup
{
    public double CameraX { get; init; }
    public double CameraY { get; init; }
    public double CameraZ { get; init; }

    /// <summary>Heading in degrees, 0 looking along +X and increasing toward +Y.</summary>
    public double YawDegrees { get; init; }

    /// <summary>Elevation of the view in degrees, positive looking up.</summary>
    public double PitchDegrees { get; init; }

    /// <summary>Vertical field of view in degrees.</summary>
    public double FieldOfViewDegrees { get; init; } = 45;

    public LightingScenario Scenario { get; init; } = LightingScenario.Sunny;

    /// <summary>Day of the year for the sun (1 = January 1).</summary>
    public int DayOfYear { get; init; } = 172;

    /// <summary>Local solar time in hours.</summary>
    public double SolarHour { get; init; } = 10;

    public double Latitude { get; init; } = 12.1;

    /// <summary>Cloud cover, 0–1.</summary>
    public double CloudCover { get; init; } = 0.3;

    public bool Haze { get; init; } = true;

    /// <summary>Null lets the panel choose the quality for the machine (AUTO).</summary>
    public RenderQuality? Quality { get; init; }

    /// <summary>Output size in pixels; zero renders at the size of the view.</summary>
    public int Width { get; init; }
    public int Height { get; init; }

    public ToneMapper Look { get; init; } = ToneMapper.AgX;

    /// <summary>Exposure correction in stops, on top of the scenario's own.</summary>
    public double ExposureStops { get; init; }

    /// <summary>White balance, −1 (cool) to 1 (warm).</summary>
    public double Temperature { get; init; }

    public bool AutoExposure { get; init; } = true;
    public bool Bloom { get; init; } = true;
    public bool Vignette { get; init; }
    public bool CorrectVerticals { get; init; } = true;

    /// <summary>Depth of field, 0 (none) to 100.</summary>
    public double DepthOfField { get; init; }

    public bool ArtificialLights { get; init; } = true;

    /// <summary>Rejects values the Render panel cannot show.</summary>
    public void Validate()
    {
        double[] finite = [CameraX, CameraY, CameraZ, YawDegrees, PitchDegrees, FieldOfViewDegrees, SolarHour, Latitude,
            CloudCover, ExposureStops, Temperature, DepthOfField];
        if (finite.Any(value => !double.IsFinite(value)) || Math.Abs(CameraX) > 1e6 || Math.Abs(CameraY) > 1e6 ||
            Math.Abs(CameraZ) > 1e6 || PitchDegrees is < -90 or > 90 || FieldOfViewDegrees is < 5 or > 150 ||
            !Enum.IsDefined(Scenario) || DayOfYear is < 1 or > 366 || SolarHour is < 0 or > 24 || Latitude is < -90 or > 90 ||
            CloudCover is < 0 or > 1 || (Quality is { } quality && !Enum.IsDefined(quality)) ||
            Width is < 0 or > 16384 || Height is < 0 or > 16384 || (Width == 0) != (Height == 0) || !Enum.IsDefined(Look) ||
            ExposureStops is < -3 or > 3 || Temperature is < -1 or > 1 || DepthOfField is < 0 or > 100)
            throw new InvalidDataException("The saved render setup is invalid.");
    }
}
