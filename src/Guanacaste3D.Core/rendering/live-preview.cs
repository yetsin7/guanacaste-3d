namespace Guanacaste3D.Core.Rendering;

/// <summary>
/// A navigable, low-detail ray-traced preview. While the camera moves it renders one sample per frame and shows it
/// denoised; when the camera stops it keeps refining up to <see cref="MaximumSamples"/> and then goes idle, using no
/// processor time. It runs on one background thread, never blocks the caller, drops frames instead of queuing them,
/// and can be suspended (freeing its memory) while a final render runs.
/// </summary>
public sealed class LivePreview : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _worker;

    private RenderScene? _scene;
    private RenderCamera _camera = new(new System.Numerics.Vector3(10, -10, 6), System.Numerics.Vector3.Zero, 45);
    private RenderSettings _settings = new();
    private ImageFinish _finish = new();
    private int _width = 480;
    private int _height = 270;
    private bool _sceneDirty;
    private bool _cameraDirty;
    private bool _settingsDirty;
    private bool _sizeDirty;
    private bool _finishDirty;
    private bool _suspended;

    /// <summary>Samples per pixel at which refinement stops and the preview goes idle.</summary>
    public int MaximumSamples { get; set; } = 40;

    /// <summary>Raised on the worker thread with each new picture; handlers must copy what they keep.</summary>
    public event Action<RenderFrame>? FrameReady;

    /// <summary>Raised with "Render.Preparing" while the hierarchy is built and "Render.Preview" once pictures flow.</summary>
    public event Action<string>? PhaseChanged;

    /// <summary>Raised when preparing the scene fails (for example, not enough memory); the preview stays idle.</summary>
    public event Action<Exception>? Failed;

    public void Start()
    {
        lock (_gate)
        {
            _worker ??= Task.Factory.StartNew(Loop, TaskCreationOptions.LongRunning);
        }
    }

    public void SetScene(RenderScene scene)
    {
        lock (_gate)
        {
            _scene = scene;
            _sceneDirty = true;
        }
        _wake.Release();
    }

    public void SetCamera(RenderCamera camera)
    {
        lock (_gate)
        {
            _camera = camera;
            _cameraDirty = true;
        }
        _wake.Release();
    }

    public void SetSettings(RenderSettings settings)
    {
        lock (_gate)
        {
            _settings = settings;
            _settingsDirty = true;
        }
        _wake.Release();
    }

    /// <summary>Changes the picture finish (exposure, bloom…); the current samples are kept and only re-finished.</summary>
    public void SetFinish(ImageFinish finish)
    {
        lock (_gate)
        {
            _finish = finish;
            _finishDirty = true;
        }
        _wake.Release();
    }

    public void SetSize(int width, int height)
    {
        lock (_gate)
        {
            var w = Math.Clamp(width, 64, 1920);
            var h = Math.Clamp(height, 48, 1080);
            if (w == _width && h == _height) return;
            (_width, _height) = (w, h);
            _sizeDirty = true;
        }
        _wake.Release();
    }

    /// <summary>Stops rendering and releases the scene's working memory until <see cref="Resume"/>.</summary>
    public void Suspend()
    {
        lock (_gate) _suspended = true;
        _wake.Release();
    }

    public void Resume()
    {
        lock (_gate)
        {
            _suspended = false;
            _sceneDirty = _scene is not null;
        }
        _wake.Release();
    }

    private void Loop()
    {
        var token = _lifetime.Token;
        PathTracer? tracer = null;
        var announced = false;
        var lastFrame = DateTime.MinValue;
        try
        {
            while (!token.IsCancellationRequested)
            {
                bool needsTracer, cameraChanged, finishChanged, settingsChanged, sizeChanged;
                RenderScene? scene;
                RenderCamera camera;
                RenderSettings settings;
                ImageFinish finish;
                int width, height;
                lock (_gate)
                {
                    if (_suspended)
                    {
                        tracer = null;
                        announced = false;
                    }
                    scene = _scene;
                    camera = _camera;
                    settings = _settings;
                    finish = _finish;
                    width = _width;
                    height = _height;
                    // Only new geometry needs a new hierarchy; lighting and size changes reuse the existing one.
                    needsTracer = !_suspended && scene is not null && (tracer is null || _sceneDirty);
                    settingsChanged = _settingsDirty;
                    sizeChanged = _sizeDirty;
                    cameraChanged = _cameraDirty;
                    finishChanged = _finishDirty;
                    if (needsTracer) _sceneDirty = false;
                    _settingsDirty = false;
                    _sizeDirty = false;
                    _cameraDirty = false;
                    _finishDirty = false;
                }
                if (_suspended || scene is null)
                {
                    _wake.Wait(token);
                    continue;
                }

                if (needsTracer)
                {
                    PhaseChanged?.Invoke("Render.Preparing");
                    tracer = null;
                    GC.Collect();
                    try
                    {
                        tracer = new PathTracer(scene, width, height, camera, settings with { MaxThreads = ThreadsFor(settings), AdaptiveSampling = false }, token);
                    }
                    catch (Exception ex) when (ex is OutOfMemoryException or ArgumentException or InvalidOperationException)
                    {
                        Failed?.Invoke(ex);
                        _wake.Wait(token);
                        continue;
                    }
                    MemoryTrim.AfterHeavyBuild();
                    announced = false;
                }
                else if (tracer is not null)
                {
                    if (sizeChanged) tracer.Resize(width, height);
                    if (settingsChanged) tracer.UpdateSettings(settings with { MaxThreads = ThreadsFor(settings), AdaptiveSampling = false });
                    if (cameraChanged || sizeChanged || settingsChanged) tracer.SetCamera(camera);
                }
                if (tracer is null) continue;

                var wantsMore = tracer.SampleCount < MaximumSamples;
                if (!wantsMore && !finishChanged && !needsTracer && !cameraChanged && !settingsChanged && !sizeChanged)
                {
                    // Converged and nothing changed: sleep until the next request instead of spinning.
                    _wake.Wait(token);
                    continue;
                }
                if (wantsMore) tracer.RenderPass(token);
                if (!announced)
                {
                    PhaseChanged?.Invoke("Render.Preview");
                    announced = true;
                }

                // While moving, show every frame quickly; while refining, spare the processor between pictures.
                var settled = tracer.SampleCount > 4;
                var interval = TimeSpan.FromMilliseconds(settled ? 120 : 35);
                if (tracer.SampleCount == 1 || finishChanged || DateTime.UtcNow - lastFrame >= interval || tracer.SampleCount >= MaximumSamples)
                {
                    var bytes = ImageProcessor.ToBgra32(tracer.LinearImage(denoise: true), tracer.Width, tracer.Height,
                        tracer.BaseExposure, finish, tracer.CaptureFeatures);
                    lastFrame = DateTime.UtcNow;
                    FrameReady?.Invoke(new RenderFrame(bytes, tracer.Width, tracer.Height, tracer.SampleCount, MaximumSamples,
                        TimeSpan.Zero, null, 1, false));
                }
                // Drain extra wake signals so one burst of input does not cause a burst of idle loops.
                while (_wake.CurrentCount > 1) _wake.Wait(0);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>A preview should leave the computer comfortable: half the cores, at least one.</summary>
    private static int ThreadsFor(RenderSettings settings) =>
        settings.MaxThreads > 0 ? settings.MaxThreads : RenderPresets.Threads(ResourceMode.Eco);

    public void Dispose()
    {
        _lifetime.Cancel();
        _wake.Release();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
        }
        _lifetime.Dispose();
        _wake.Dispose();
    }
}
