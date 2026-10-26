using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop.Controls;

/// <summary>What the render panel needs from the window that hosts it.</summary>
internal interface IRenderSource
{
    /// <summary>The camera shared with the 3D view, so both always show the same place.</summary>
    NavigationCamera Camera { get; }

    string ProjectName { get; }

    (Vector3 Min, Vector3 Max) SceneBounds();

    /// <summary>The current scene, built in the background and cached; null when the project is empty.</summary>
    Task<RenderScene?> GetSceneAsync();

    /// <summary>Ray queries for the current scene, or null until they are built.</summary>
    SceneQuery? Query { get; }

    /// <summary>Raised when geometry, links or materials change and the scene must be rebuilt.</summary>
    event Action? SceneChanged;

    /// <summary>Raised when the scene query becomes available or changes.</summary>
    event Action? QueryChanged;
}

/// <summary>
/// The Render tab: a live, navigable ray-traced preview, and the final render. Moving the camera shows lighting and
/// shadows at low detail; pressing Render freezes the picture while a high-quality image (or a walk-through video) is
/// generated. Rendering is protected: it is fitted to the free memory beforehand and stopped, keeping the picture so
/// far, if memory runs short.
/// </summary>
public partial class RenderPanel : UserControl
{
    private IRenderSource? _source;
    private SceneNavigator? _navigator;
    private LivePreview? _preview;
    private WriteableBitmap? _bitmap;
    private int _framePending;
    private bool _ready;

    private RenderSession? _session;
    private CancellationTokenSource? _cancel;
    private bool _rendering;
    private RenderFrame? _lastFinal;
    private bool _showingFinal;
    private long _sceneTriangles;
    private long _sceneBytes;

    private readonly CameraPath _path = new();
    private DispatcherTimer? _tourTimer;
    private DateTime _tourStart;
    private bool _renderingTour;

    public RenderPanel()
    {
        InitializeComponent();
        SunDatePicker.SelectedDate = DateTime.Today;
        _ready = true;
        IsVisibleChanged += (_, _) => OnVisibilityChanged();
        Unloaded += (_, _) => Shutdown();
        UpdateMemoryEstimate();
    }

    // ---------------------------------------------------------------- wiring

    internal void Attach(IRenderSource source)
    {
        _source = source;
        _navigator = new SceneNavigator(ImageHost, source.Camera)
        {
            Bounds = source.SceneBounds,
            Pick = (origin, direction) => source.Query?.Pick(origin, direction)
        };
        _navigator.Changed += OnCameraChanged;
        source.SceneChanged += () => Dispatcher.BeginInvoke(RefreshScene);
        source.QueryChanged += () => Dispatcher.BeginInvoke(ApplyCollision);
        ApplyCollision();
    }

    /// <summary>Lets the main window reuse the navigator's collision and key handling.</summary>
    internal SceneNavigator? Navigator => _navigator;

    private void ApplyCollision()
    {
        if (_navigator is null || _source is null) return;
        _navigator.Collision = _source.Query?.Collision();
        _navigator.CollisionEnabled = CollisionCheck.IsChecked == true;
    }

    private void OnVisibilityChanged()
    {
        if (!IsVisible)
        {
            _preview?.Suspend();
            StopTour();
            return;
        }
        if (_preview is null && PreviewToggle.IsChecked == true) StartPreview();
        else if (PreviewToggle.IsChecked == true && !_showingFinal) ResumePreview();
        UpdateMemoryEstimate();
    }

    private void Shutdown()
    {
        _cancel?.Cancel();
        StopTour();
        _preview?.Dispose();
        _preview = null;
        _session?.Dispose();
        _session = null;
    }

    // ---------------------------------------------------------------- live preview

    private void StartPreview()
    {
        if (_source is null) return;
        _preview = new LivePreview { MaximumSamples = 40 };
        _preview.FrameReady += OnPreviewFrame;
        _preview.PhaseChanged += phase => Dispatcher.BeginInvoke(() =>
        {
            if (!_showingFinal && !_rendering)
                StatusText.Text = UiText.Get(phase == "Render.Preparing" ? "RenderPanel.PreparingScene" : "RenderPanel.Idle");
        });
        _preview.Failed += _ => Dispatcher.BeginInvoke(() => StatusText.Text = UiText.Get("RenderPanel.PreviewFailed"));
        _preview.Start();
        PushPreviewState();
        RefreshScene();
    }

    private void ResumePreview()
    {
        if (_preview is null)
        {
            StartPreview();
            return;
        }
        PushPreviewState();
        _preview.Resume();
        RefreshScene();
    }

    private void PushPreviewState()
    {
        if (_preview is null) return;
        var (width, height) = PreviewSize();
        _preview.SetSize(width, height);
        _preview.SetSettings(PreviewSettings());
        _preview.SetFinish(CurrentFinish());
        _preview.SetCamera(CurrentCamera());
    }

    /// <summary>Refreshes the preview scene on the UI dispatcher because scene notifications may arrive on worker threads.</summary>
    private async void RefreshScene()
    {
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke((Action)RefreshScene);
            return;
        }
        if (_source is null || !IsVisible) return;
        var scene = await _source.GetSceneAsync();
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke((Action)(() => ApplyPreviewScene(scene)));
            return;
        }
        ApplyPreviewScene(scene);
    }

    /// <summary>Applies an asynchronously built scene only after returning to its owning UI dispatcher.</summary>
    private void ApplyPreviewScene(RenderScene? scene)
    {
        if (scene is null)
        {
            _sceneTriangles = 0;
            if (!_rendering && !_showingFinal) StatusText.Text = UiText.Get("RenderPanel.EmptyScene");
            return;
        }
        _sceneTriangles = scene.TriangleCount;
        _sceneBytes = scene.EstimatedBytes;
        UpdateMemoryEstimate();
        if (PreviewToggle.IsChecked == true && !_showingFinal && !_rendering) _preview?.SetScene(scene);
    }

    private void OnPreviewFrame(RenderFrame frame)
    {
        // Drop the picture if the previous one has not been shown yet: the newest always wins.
        if (Interlocked.Exchange(ref _framePending, 1) == 1) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            Volatile.Write(ref _framePending, 0);
            if (_showingFinal || _rendering) return;
            Show(frame);
            StatusText.Text = frame.Samples >= frame.TargetSamples
                ? UiText.Format("RenderPanel.PreviewReady", frame.Samples)
                : UiText.Format("RenderPanel.PreviewStatus", frame.Samples, frame.TargetSamples);
        });
    }

    private void Show(RenderFrame frame)
    {
        if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
        {
            _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
            PreviewImage.Source = _bitmap;
        }
        _bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Bgra, frame.Width * 4, 0);
    }

    private (int Width, int Height) PreviewSize()
    {
        var aspect = ImageHost.ActualHeight > 0 ? ImageHost.ActualWidth / ImageHost.ActualHeight : 16.0 / 9;
        var height = Mode(ResourcePicker) switch { "Eco" => 216, "Maximum" => 360, _ => 288 };
        return ((int)Math.Round(height * Math.Clamp(aspect, 0.5, 3)), height);
    }

    private void ImageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_ready) return;
        if (_preview is not null)
        {
            var (width, height) = PreviewSize();
            _preview.SetSize(width, height);
        }
        UpdateMemoryEstimate();
    }

    // ---------------------------------------------------------------- control state

    private static string Mode(ComboBox picker) => (picker.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    private LightingScenario Scenario() => Enum.TryParse<LightingScenario>(Mode(ScenarioPicker), out var value) ? value : LightingScenario.Sunny;

    private RenderQuality Quality() => Enum.TryParse<RenderQuality>(Mode(QualityPicker), out var value) ? value : RenderQuality.Standard;

    private ResourceMode ResourceUse() => Enum.TryParse<ResourceMode>(Mode(ResourcePicker), out var value) ? value : ResourceMode.Balanced;

    private double Latitude()
    {
        var text = LatitudeInput.Text.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= -90 and <= 90 ? value : 12.1;
    }

    private RenderSettings LightingFor(RenderQuality quality, ResourceMode resources) =>
        RenderPresets.Lighting(Scenario(), Latitude(), (SunDatePicker.SelectedDate ?? DateTime.Today).DayOfYear, HourSlider.Value,
            quality, resources);

    private RenderSettings PreviewSettings() => LightingFor(RenderQuality.Draft, ResourceMode.Eco) with
    {
        MaxBounces = 2, MaxThreads = RenderPresets.Threads(ResourceUse() == ResourceMode.Maximum ? ResourceMode.Balanced : ResourceMode.Eco)
    };

    private ImageFinish CurrentFinish()
    {
        var baseFinish = RenderPresets.Finish(Scenario());
        return baseFinish with
        {
            AutoExposure = AutoExposureCheck.IsChecked == true,
            ExposureStops = baseFinish.ExposureStops + (float)ExposureSlider.Value,
            Bloom = BloomCheck.IsChecked == true ? Math.Max(0.12f, baseFinish.Bloom) : 0,
            Vignette = VignetteCheck.IsChecked == true ? 0.12f : 0
        };
    }

    private RenderCamera CurrentCamera()
    {
        var camera = _source?.Camera ?? new NavigationCamera();
        var focus = 10f;
        if (_source?.Query is { } query && query.Pick(camera.Position, camera.Forward) is { } distance) focus = Math.Max(0.5f, distance);
        return camera.ToRenderCamera(focus) with
        {
            CorrectVerticals = VerticalsCheck.IsChecked == true,
            ApertureRadius = (float)(DepthOfFieldSlider.Value / 100 * 0.25),
            FocusDistance = focus
        };
    }

    private (int Width, int Height) SelectedBaseSize()
    {
        var tag = Mode(SizePicker).Split('x');
        if (tag.Length == 2 && int.TryParse(tag[0], out var w) && int.TryParse(tag[1], out var h) && w > 0 && h > 0) return (w, h);
        var width = Math.Max(640, (int)ImageHost.ActualWidth);
        var height = Math.Max(360, (int)ImageHost.ActualHeight);
        return (width, height);
    }

    private void UpdateMemoryEstimate()
    {
        if (!_ready || MemoryText is null) return;
        var (baseWidth, baseHeight) = SelectedBaseSize();
        var (width, height) = RenderPresets.OutputSize(baseWidth, baseHeight, Quality());
        var memory = RenderSafety.ReadMemory();
        var estimate = RenderSafety.EstimateBytes(width, height, _sceneTriangles, _sceneBytes);
        MemoryText.Text = UiText.Format("RenderPanel.MemoryEstimate", UiText.Bytes(estimate), UiText.Bytes(memory.AvailableBytes)) +
                          $" · {width}×{height}";
    }

    private void OnCameraChanged()
    {
        if (_source is null) return;
        if (_showingFinal && !_rendering)
        {
            // Moving again leaves the finished picture and returns to the live preview.
            _showingFinal = false;
            if (PreviewToggle.IsChecked == true) ResumePreview();
        }
        _preview?.SetCamera(CurrentCamera());
    }

    // ---------------------------------------------------------------- event handlers

    private void PreviewToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (PreviewToggle.IsChecked == true)
        {
            _showingFinal = false;
            if (IsVisible) ResumePreview();
        }
        else
        {
            _preview?.Suspend();
        }
    }

    private void Lighting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        SunControls.Visibility = Scenario() == LightingScenario.Sunny ? Visibility.Visible : Visibility.Collapsed;
        _preview?.SetSettings(PreviewSettings());
        _preview?.SetFinish(CurrentFinish());
        if (_showingFinal) RefinishFinal();
    }

    private void Lighting_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Lighting_Changed(sender, e);

    private void Latitude_Changed(object sender, TextChangedEventArgs e) => Lighting_Changed(sender, e);

    private void Quality_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateMemoryEstimate();
        if (_preview is not null)
        {
            var (width, height) = PreviewSize();
            _preview.SetSize(width, height);
            _preview.SetSettings(PreviewSettings());
        }
    }

    private void Finish_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _preview?.SetFinish(CurrentFinish());
        if (_showingFinal) RefinishFinal();
    }

    private void Finish_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Finish_Changed(sender, e);

    private void Camera_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _preview?.SetCamera(CurrentCamera());
    }

    private void Camera_SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Camera_Changed(sender, e);

    private void Collision_Changed(object sender, RoutedEventArgs e)
    {
        if (_navigator is not null) _navigator.CollisionEnabled = CollisionCheck.IsChecked == true;
    }

    private void FrameAll_Click(object sender, RoutedEventArgs e) => _navigator?.FrameAll();

    /// <summary>Re-applies exposure and grading to a finished render without rendering again.</summary>
    private async void RefinishFinal()
    {
        if (_session is null || _rendering) return;
        var session = _session;
        var finish = CurrentFinish();
        var frame = await Task.Run(() => session.Refinish(finish, _lastFinal?.Elapsed ?? TimeSpan.Zero));
        if (frame is null || session != _session) return;
        _lastFinal = frame with { IsFinal = true };
        Show(frame);
    }

    // ---------------------------------------------------------------- final render

    private async void RenderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rendering || _renderingTour)
        {
            _cancel?.Cancel();
            return;
        }
        await RenderFinalAsync();
    }

    /// <summary>Starts a final render with the panel's settings (used by the Render buttons elsewhere in the window).</summary>
    internal async void RequestRender()
    {
        if (_rendering || _renderingTour) return;
        await RenderFinalAsync();
    }

    /// <summary>Stops any render or video in progress; the picture so far is kept.</summary>
    internal void CancelAll() => _cancel?.Cancel();

    private async Task RenderFinalAsync()
    {
        if (_source is null) return;
        var scene = await _source.GetSceneAsync();
        if (scene is null)
        {
            StatusText.Text = UiText.Get("RenderPanel.EmptyScene");
            return;
        }

        var quality = Quality();
        var profile = RenderPresets.Profile(quality);
        var (baseWidth, baseHeight) = SelectedBaseSize();
        var (width, height) = RenderPresets.OutputSize(baseWidth, baseHeight, quality);
        var fit = RenderSafety.FitToMemory(width, height, scene.TriangleCount, scene.EstimatedBytes, RenderSafety.ReadMemory());
        if (!fit.Fits)
        {
            StatusText.Text = UiText.Get("RenderPanel.NoMemory");
            return;
        }
        if (fit.Reduced) StatusText.Text = UiText.Format("RenderPanel.SizeReduced", fit.Width, fit.Height);

        var settings = LightingFor(quality, ResourceUse());
        var camera = CurrentCamera();
        var job = new RenderJob(fit.Width, fit.Height, profile.TargetSamples, null, true, CurrentFinish());

        // The preview stops and gives its memory back: the picture is frozen while the final render is made.
        _preview?.Suspend();
        _session?.Dispose();
        _session = new RenderSession(scene, camera, settings, job);
        _cancel = new CancellationTokenSource();
        SetRendering(true);
        _showingFinal = true;
        var session = _session;
        session.PhaseChanged += phase => Dispatcher.BeginInvoke(() =>
        {
            if (_rendering) StatusText.Text = UiText.Get(phase == "Render.Preparing" ? "RenderPanel.PreparingScene" : "RenderPanel.Idle");
        });
        session.FrameReady += frame => Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            if (session != _session) return;
            Show(frame);
            RenderProgress.Value = Math.Min(1, frame.Samples / (double)Math.Max(1, frame.TargetSamples));
            if (!frame.IsFinal)
                StatusText.Text = UiText.Format("RenderPanel.Rendering", frame.Samples, frame.TargetSamples, frame.Width, frame.Height,
                    frame.Remaining is { } remaining ? UiText.Duration(remaining) : "…");
        });

        RenderResult? result = null;
        try
        {
            result = await session.RunAsync(_cancel.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText.Text = UiText.Format("RenderPanel.Failed", ex.Message);
        }
        finally
        {
            SetRendering(false);
        }
        if (result is null) return;

        _lastFinal = result.LastFrame;
        SaveButton.IsEnabled = CopyButton.IsEnabled = _lastFinal is not null;
        if (result.StoppedForMemory)
            StatusText.Text = UiText.Format("RenderPanel.MemoryStop", result.Samples);
        else if (result.Cancelled)
            StatusText.Text = UiText.Format("RenderPanel.RenderStopped", result.Samples);
        else
            StatusText.Text = UiText.Format("RenderPanel.Rendered", result.Samples, session.Tracer?.Width ?? fit.Width,
                session.Tracer?.Height ?? fit.Height, UiText.Duration(result.Elapsed), result.MegaRaysPerSecond);
        if (result.StoppedForMemory)
        {
            // Let go of the working buffers; the picture itself is already kept.
            session.Dispose();
            _session = null;
            GC.Collect();
        }
    }

    private void SetRendering(bool rendering)
    {
        _rendering = rendering;
        RenderButton.Content = UiText.Get(rendering ? "RenderPanel.Stop" : "RenderPanel.Render");
        PauseButton.IsEnabled = rendering;
        PauseButton.Content = UiText.Get("RenderPanel.Pause");
        RenderProgress.Visibility = rendering ? Visibility.Visible : Visibility.Collapsed;
        RenderProgress.Value = 0;
        PreviewToggle.IsEnabled = !rendering;
        RenderTourButton.IsEnabled = !rendering;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || !_rendering) return;
        if (_session.IsPaused)
        {
            _session.Resume();
            PauseButton.Content = UiText.Get("RenderPanel.Pause");
        }
        else
        {
            _session.Pause();
            PauseButton.Content = UiText.Get("RenderPanel.Resume");
        }
    }

    // ---------------------------------------------------------------- saving

    private BitmapSource? CurrentImage(bool opaque)
    {
        if (_lastFinal is not { } frame) return null;
        var source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Bgra, frame.Width * 4);
        if (!opaque) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
        converted.Freeze();
        return converted;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFinal is null)
        {
            StatusText.Text = UiText.Get("RenderPanel.NothingToSave");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = UiText.Get("RenderPanel.ImageFilter"),
            FileName = SafeName(_source?.ProjectName ?? "render"),
            AddExtension = true,
            DefaultExt = ".png"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            var jpeg = dialog.FilterIndex == 2 || Path.GetExtension(dialog.FileName).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                       Path.GetExtension(dialog.FileName).Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
            BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(CurrentImage(opaque: jpeg)!));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
            StatusText.Text = UiText.Format("RenderPanel.Saved", dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, UiText.Get("Dialog.SaveFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentImage(opaque: true) is not { } image)
        {
            StatusText.Text = UiText.Get("RenderPanel.NothingToSave");
            return;
        }
        try
        {
            Clipboard.SetImage(image);
            StatusText.Text = UiText.Get("RenderPanel.Copied");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another program holds the clipboard; the user can simply try again.
        }
    }

    private static string SafeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "render" : name;
    }

    // ---------------------------------------------------------------- walk-through

    private float MarkSeconds()
    {
        var text = MarkSecondsInput.Text.Replace(',', '.');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is > 0.05f and < 600 ? value : 4;
    }

    private void MarkSeconds_LostFocus(object sender, RoutedEventArgs e) =>
        MarkSecondsInput.Text = MarkSeconds().ToString("0.##", CultureInfo.InvariantCulture);

    private void AddMark_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null) return;
        var camera = _source.Camera;
        var focus = _source.Query?.Pick(camera.Position, camera.Forward) is { } distance ? Math.Max(1f, distance) : 10f;
        _path.Add(new CameraMark(camera.Position, camera.Position + camera.Forward * focus, camera.FieldOfViewDegrees, MarkSeconds()));
        RefreshMarks();
        MarksList.SelectedIndex = _path.Marks.Count - 1;
    }

    private void RefreshMarks()
    {
        var selected = MarksList.SelectedIndex;
        MarksList.Items.Clear();
        for (var i = 0; i < _path.Marks.Count; i++)
            MarksList.Items.Add(UiText.Format("RenderPanel.MarkName", i + 1, i == 0 ? 0 : _path.Marks[i].SecondsFromPrevious));
        MarksList.SelectedIndex = Math.Min(selected, MarksList.Items.Count - 1);
    }

    private void MarksList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        GoToMarkButton.IsEnabled = RemoveMarkButton.IsEnabled = MarksList.SelectedIndex >= 0;

    private void GoToMark_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null || MarksList.SelectedIndex < 0) return;
        var mark = _path.Marks[MarksList.SelectedIndex];
        ApplyCamera(mark.Position, mark.Target, mark.FieldOfViewDegrees);
    }

    private void RemoveMark_Click(object sender, RoutedEventArgs e)
    {
        if (MarksList.SelectedIndex < 0) return;
        _path.RemoveAt(MarksList.SelectedIndex);
        RefreshMarks();
    }

    private void ApplyCamera(Vector3 position, Vector3 target, float fieldOfView)
    {
        if (_source is null) return;
        _source.Camera.Position = position;
        _source.Camera.FieldOfViewDegrees = fieldOfView;
        _source.Camera.LookAt(target);
        OnCameraChanged();
    }

    private void PlayTour_Click(object sender, RoutedEventArgs e)
    {
        if (_tourTimer is not null)
        {
            StopTour();
            return;
        }
        if (_path.Marks.Count < 2)
        {
            StatusText.Text = UiText.Get("RenderPanel.TourNeedsMarks");
            return;
        }
        _tourStart = DateTime.UtcNow;
        PlayTourButton.Content = UiText.Get("RenderPanel.StopTour");
        _tourTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _tourTimer.Tick += (_, _) =>
        {
            var seconds = (float)(DateTime.UtcNow - _tourStart).TotalSeconds;
            var (position, target, fieldOfView) = _path.Sample(seconds);
            ApplyCamera(position, target, fieldOfView);
            if (seconds >= _path.Duration) StopTour();
        };
        _tourTimer.Start();
    }

    private void StopTour()
    {
        _tourTimer?.Stop();
        _tourTimer = null;
        PlayTourButton.Content = UiText.Get("RenderPanel.PlayTour");
    }

    private async void RenderTour_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null || _rendering || _renderingTour) return;
        if (_path.Marks.Count < 2)
        {
            StatusText.Text = UiText.Get("RenderPanel.TourNeedsMarks");
            return;
        }
        var dialog = new OpenFolderDialog { Title = UiText.Get("RenderPanel.RenderVideo") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var scene = await _source.GetSceneAsync();
        if (scene is null) return;

        var quality = Quality();
        var (baseWidth, baseHeight) = SelectedBaseSize();
        var (width, height) = RenderPresets.OutputSize(baseWidth, baseHeight, quality);
        var fit = RenderSafety.FitToMemory(width, height, scene.TriangleCount, scene.EstimatedBytes, RenderSafety.ReadMemory());
        if (!fit.Fits)
        {
            StatusText.Text = UiText.Get("RenderPanel.NoMemory");
            return;
        }
        var fps = int.TryParse((FpsPicker.SelectedItem as ComboBoxItem)?.Tag as string, out var chosen) ? chosen : 30;
        var folder = Path.Combine(dialog.FolderName, SafeName(_source.ProjectName) + "-recorrido");
        // Video frames need far fewer samples than a still: the next frame is nearly the same picture.
        var samples = Math.Max(12, RenderPresets.Profile(quality).TargetSamples / 4);

        _preview?.Suspend();
        _renderingTour = true;
        _showingFinal = true;
        _cancel = new CancellationTokenSource();
        RenderButton.Content = UiText.Get("RenderPanel.Stop");
        RenderTourButton.IsEnabled = false;
        RenderProgress.Visibility = Visibility.Visible;
        var progress = new Progress<TourProgress>(p =>
        {
            RenderProgress.Value = p.Frame / (double)Math.Max(1, p.Total);
            StatusText.Text = UiText.Format("RenderPanel.TourFrame", p.Frame, p.Total, p.Seconds);
        });
        TourResult? result = null;
        try
        {
            result = await TourRenderer.RenderAsync(scene, _path, LightingFor(quality, ResourceUse()), fit.Width, fit.Height, fps, samples,
                CurrentFinish(), folder, progress, _cancel.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = UiText.Format("RenderPanel.Failed", ex.Message);
        }
        finally
        {
            _renderingTour = false;
            RenderButton.Content = UiText.Get("RenderPanel.Render");
            RenderTourButton.IsEnabled = true;
            RenderProgress.Visibility = Visibility.Collapsed;
        }
        if (result is null) return;
        StatusText.Text = result.FramesWritten == result.TotalFrames
            ? UiText.Format("RenderPanel.TourDone", result.FramesWritten, result.Folder)
            : result.StoppedForMemory
                ? UiText.Format("RenderPanel.MemoryStop", result.FramesWritten)
                : UiText.Format("RenderPanel.TourStopped", result.FramesWritten, result.Folder);
    }
}
