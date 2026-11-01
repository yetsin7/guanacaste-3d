using System.Globalization;
using System.Windows.Controls;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;

namespace Guanacaste3D.Desktop.Controls;

public partial class RenderPanel
{
    /// <summary>Loads an existing setup without an associated file (e.g., an in-memory project).</summary>
    public void ApplySetup(RenderSetup setup) => ApplySetupForProject(setup, null);

    /// <summary>Restores project render controls, including a saved and safely resolved HDRI reference.</summary>
    public void ApplySetupForProject(RenderSetup setup, string? projectFile)
    {
        ClearHdriForDocument();
        // First the style, which sets the look, white balance, bloom, vignette and effects; the saved values follow.
        Select(StylePicker, setup.StyleId);
        Select(ScenarioPicker, setup.Scenario.ToString());
        SunDatePicker.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1).AddDays(setup.DayOfYear - 1);
        HourSlider.Value = Math.Clamp(setup.SolarHour, HourSlider.Minimum, HourSlider.Maximum);
        CloudSlider.Value = setup.CloudCover * 100;
        LatitudeInput.Text = setup.Latitude.ToString("0.###", CultureInfo.InvariantCulture);
        HazeCheck.IsChecked = setup.Haze;
        Select(QualityPicker, setup.Quality?.ToString() ?? "Auto");
        Select(SizePicker, $"{setup.Width}x{setup.Height}");
        Select(BucketSizePicker, setup.BucketSize.ToString(CultureInfo.InvariantCulture));
        Select(LookPicker, setup.Look switch { ToneMapper.Aces => "Aces", ToneMapper.Soft => "Soft", _ => "AgX" });
        ExposureSlider.Value = setup.ExposureStops;
        TemperatureSlider.Value = setup.Temperature;
        AutoExposureCheck.IsChecked = setup.AutoExposure;
        BloomCheck.IsChecked = setup.Bloom;
        VignetteCheck.IsChecked = setup.Vignette;
        VerticalsCheck.IsChecked = setup.CorrectVerticals;
        DepthOfFieldSlider.Value = setup.DepthOfField;
        ArtificialLightsCheck.IsChecked = setup.ArtificialLights;
        StoneDetailCheck.IsChecked = setup.StoneMicrogeometry;
        HdriYawSlider.Value = setup.EnvironmentYawDegrees;
        HdriExposureSlider.Value = setup.EnvironmentExposureStops;
        Select(CloudStylePicker, setup.Clouds.ToString());
        // The saved sliders win over the style's own values, set when the style was selected first.
        HighlightsSlider.Value = setup.Highlights;
        ShadowsSlider.Value = setup.Shadows;
        VibranceSlider.Value = setup.Vibrance;
        TintSlider.Value = setup.Tint;
        GlareSlider.Value = setup.Glare;
        FogSlider.Value = setup.Fog;
        SharpenSlider.Value = setup.Sharpen;
        AberrationSlider.Value = setup.ChromaticAberration;
        GrainSlider.Value = setup.FilmGrain;
        MonochromeCheck.IsChecked = setup.Monochrome;
        Select(SkyPicker, string.Empty);
        RestoreHdriForProject(setup.EnvironmentPath, projectFile);
        OnCameraChanged();
    }

    /// <summary>The panel's current settings and the given camera, as a setup the project can keep.</summary>
    public RenderSetup CaptureSetup(NavigationCamera camera)
    {
        var size = Mode(SizePicker).Split('x');
        var width = size.Length == 2 && int.TryParse(size[0], out var w) ? w : 0;
        var height = size.Length == 2 && int.TryParse(size[1], out var h) ? h : 0;
        return new RenderSetup
        {
            CameraX = camera.Position.X, CameraY = camera.Position.Y, CameraZ = camera.Position.Z,
            YawDegrees = camera.YawDegrees, PitchDegrees = camera.PitchDegrees,
            FieldOfViewDegrees = Math.Clamp(camera.FieldOfViewDegrees, 5, 150),
            Scenario = Scenario(), DayOfYear = (SunDatePicker.SelectedDate ?? DateTime.Today).DayOfYear,
            SolarHour = HourSlider.Value, Latitude = Latitude(), CloudCover = CloudSlider.Value / 100,
            Haze = HazeCheck.IsChecked == true, Quality = RequestedQuality(),
            Width = width > 0 && height > 0 ? width : 0, Height = width > 0 && height > 0 ? height : 0,
            BucketSize = int.TryParse(Mode(BucketSizePicker), out var bucketSize) ? bucketSize : 0,
            Look = CurrentFinish().ToneMapper, ExposureStops = ExposureSlider.Value, Temperature = TemperatureSlider.Value,
            AutoExposure = AutoExposureCheck.IsChecked == true, Bloom = BloomCheck.IsChecked == true,
            Vignette = VignetteCheck.IsChecked == true, CorrectVerticals = VerticalsCheck.IsChecked == true,
            DepthOfField = DepthOfFieldSlider.Value, ArtificialLights = ArtificialLightsCheck.IsChecked == true,
            StoneMicrogeometry = StoneDetailCheck.IsChecked == true,
            EnvironmentPath = _hdriFilePath,
            EnvironmentYawDegrees = HdriYawSlider.Value,
            EnvironmentExposureStops = HdriExposureSlider.Value,
            Clouds = SelectedClouds(), StyleId = SelectedStyle(), Highlights = HighlightsSlider.Value, Shadows = ShadowsSlider.Value,
            Vibrance = VibranceSlider.Value, Tint = TintSlider.Value, Glare = GlareSlider.Value, Fog = FogSlider.Value,
            Sharpen = SharpenSlider.Value, ChromaticAberration = AberrationSlider.Value, FilmGrain = GrainSlider.Value,
            Monochrome = MonochromeCheck.IsChecked == true
        };
    }

    /// <summary>Save As uses the target filename, keeping HDRIs inside the project folder portable.</summary>
    public RenderSetup CaptureSetupForProject(NavigationCamera camera, string projectFile)
    {
        var setup = CaptureSetup(camera);
        if (setup.EnvironmentPath is not { } absolute) return setup;
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile));
            if (folder is null) return setup;
            var local = Path.GetRelativePath(folder, absolute);
            if (!Path.IsPathRooted(local) && local != ".." &&
                !local.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !IsNetworkHdriPath(local))
                return setup with { EnvironmentPath = local };
        }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException) { }
        return setup;
    }

    /// <summary>Selects the entry whose tag matches, leaving the picker unchanged when none does.</summary>
    private static void Select(ComboBox picker, string tag)
    {
        var item = picker.Items.OfType<ComboBoxItem>().FirstOrDefault(entry => entry.Tag as string == tag);
        if (item is not null) picker.SelectedItem = item;
    }
}
