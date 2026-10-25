using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop;

public partial class MainWindow
{
    private const int RenderTabIndex = 6;
    private float _orbitAzimuth = -50;
    private float _orbitElevation = 28;
    private float _orbitDistance = 1;
    private Point? _orbitDragStart;
    private CancellationTokenSource? _renderCancellation;
    private WriteableBitmap? _renderBitmap;

    /// <summary>Returns the shared orbit camera used by both the interactive view and the renderer.</summary>
    private RenderCamera CurrentRenderCamera(Vector3 min, Vector3 max) =>
        RenderCamera.FrameBounds(min, max, _orbitAzimuth, _orbitElevation, _orbitDistance);

    /// <summary>Returns model bounds, falling back to a 10 m site for an empty project.</summary>
    private (Vector3 Min, Vector3 Max) ModelBounds()
    {
        if (_session.Document.Walls.Count == 0 && _session.Document.Floors.Count == 0)
            return (new Vector3(-5, -5, 0), new Vector3(5, 5, 3));
        return BuildingMesher.Build(_session.Document).Bounds();
    }

    /// <summary>Begins an orbit drag in the 3D view.</summary>
    private void ModelView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _orbitDragStart = e.GetPosition((IInputElement)sender);
        ((UIElement)sender).CaptureMouse();
    }

    /// <summary>Orbits horizontally and vertically while dragging.</summary>
    private void ModelView_MouseMove(object sender, MouseEventArgs e)
    {
        if (_orbitDragStart is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition((IInputElement)sender);
        _orbitAzimuth -= (float)(current.X - start.X) * 0.4f;
        _orbitElevation = Math.Clamp(_orbitElevation + (float)(current.Y - start.Y) * 0.3f, -5, 89);
        _orbitDragStart = current;
        Render3D();
    }

    /// <summary>Ends an orbit drag.</summary>
    private void ModelView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _orbitDragStart = null;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    /// <summary>Dollies the orbit camera toward or away from the model.</summary>
    private void ModelView_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _orbitDistance = Math.Clamp(_orbitDistance * (e.Delta > 0 ? 0.88f : 1.14f), 0.15f, 8f);
        Render3D();
        e.Handled = true;
    }

    /// <summary>Restores the default three-quarter camera.</summary>
    private void ResetOrbit_Click(object sender, RoutedEventArgs e)
    {
        (_orbitAzimuth, _orbitElevation, _orbitDistance) = (-50, 28, 1);
        Render3D();
    }

    /// <summary>Shows the photorealistic render view.</summary>
    private void ShowRender_Click(object sender, RoutedEventArgs e) => ViewTabs.SelectedIndex = RenderTabIndex;

    /// <summary>Starts a progressive render, or stops the one in progress.</summary>
    private async void StartRender_Click(object sender, RoutedEventArgs e)
    {
        if (_renderCancellation is not null)
        {
            _renderCancellation.Cancel();
            return;
        }
        ViewTabs.SelectedIndex = RenderTabIndex;
        // The denoiser reaches a clean image with far fewer samples, so the presets stay modest.
        var quality = RenderQualityPicker.SelectedIndex switch { 0 => (0.5, 32), 2 => (1.0, 384), _ => (0.75, 128) };
        var denoise = DenoiseCheck.IsChecked == true;
        var hostWidth = Math.Max(320, RenderHost.ActualWidth);
        var hostHeight = Math.Max(200, RenderHost.ActualHeight);
        var width = Math.Max(64, (int)(hostWidth * quality.Item1));
        var height = Math.Max(48, (int)(hostHeight * quality.Item1));
        var maximumSamples = quality.Item2;

        var document = _session.Document;
        var (min, max) = ModelBounds();
        var camera = CurrentRenderCamera(min, max);
        var settings = DaylightSettings(SiteLatitude(), (SunDatePicker.SelectedDate ?? DateTime.Today).DayOfYear,
            SunHourSlider.Value);

        using var cancellation = new CancellationTokenSource();
        _renderCancellation = cancellation;
        StartRenderButton.Content = GetText("Render.Stop");
        RenderStatusText.Text = GetText("Render.Preparing");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var tracer = await Task.Run(() =>
                new PathTracer(BuildingMesher.Build(document, includeGround: true), width, height, camera, settings),
                cancellation.Token);
            _renderBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            RenderImage.Source = _renderBitmap;
            var bounds = new Int32Rect(0, 0, width, height);
            while (tracer.SampleCount < maximumSamples && !cancellation.IsCancellationRequested)
            {
                var pixels = await Task.Run(() =>
                {
                    tracer.RenderPass(cancellation.Token);
                    return tracer.ToBgra32(denoise);
                }, cancellation.Token);
                _renderBitmap.WritePixels(bounds, pixels, width * 4, 0);
                RenderStatusText.Text = FormatText("Render.Progress", tracer.SampleCount, maximumSamples,
                    width, height, stopwatch.Elapsed.TotalSeconds);
            }
            RenderStatusText.Text = FormatText("Render.Completed", tracer.SampleCount, width, height,
                stopwatch.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            RenderStatusText.Text = GetText("Render.Stopped");
        }
        finally
        {
            _renderCancellation = null;
            StartRenderButton.Content = GetText("Render.Start");
        }
    }

    /// <summary>Saves the current render as a PNG image.</summary>
    private void SaveRender_Click(object sender, RoutedEventArgs e)
    {
        if (_renderBitmap is null)
        {
            RenderStatusText.Text = GetText("Render.NothingToSave");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = GetText("Dialog.PngFilter"),
            FileName = _session.Document.Name + ".png",
            AddExtension = true,
            DefaultExt = ".png"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_renderBitmap.Clone()));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            StatusText.Text = GetText("Render.Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, LocalizedErrors.Translate(ex.Message), GetText("Dialog.ExportFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Default site latitude: Managua, Nicaragua.</summary>
    private const double DefaultLatitude = 12.1;

    /// <summary>Reads the site latitude field, falling back to Managua when it is empty or invalid.</summary>
    private double SiteLatitude()
    {
        var text = SiteLatitudeInput.Text.Replace(',', '.');
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out var latitude) &&
               latitude is >= -90 and <= 90
            ? latitude
            : DefaultLatitude;
    }

    /// <summary>Builds lighting from the real sun position; below the horizon it renders dusk sky light only.</summary>
    private static RenderSettings DaylightSettings(double latitude, int dayOfYear, double solarHour)
    {
        var sun = SolarPosition.Direction(latitude, dayOfYear, solarHour);
        var altitude = SolarPosition.AltitudeDegrees(sun);
        // Fade the sun over its last 3° above the horizon so sunrise and sunset do not pop.
        var sunFactor = (float)Math.Clamp(altitude / 3, 0, 1);
        return new RenderSettings
        {
            SunDirection = altitude > 0 ? sun : Vector3.Normalize(new Vector3(sun.X, sun.Y, 0.02f)),
            SunIntensity = new RenderSettings().SunIntensity * sunFactor,
            SkyIntensity = new RenderSettings().SkyIntensity * (altitude > 0 ? 1 : 0.15f),
            MaxThreads = Math.Max(1, Environment.ProcessorCount - 1)
        };
    }
    /// <summary>Keeps the interactive camera aspect in sync with the viewport.</summary>
    private void ModelViewport_SizeChanged(object sender, SizeChangedEventArgs e) => Render3D();

    /// <summary>Cancels any running render before the window closes.</summary>
    private void CancelRender() => _renderCancellation?.Cancel();
}
