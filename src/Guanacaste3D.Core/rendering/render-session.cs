using System.Diagnostics;

namespace Guanacaste3D.Core.Rendering;

/// <summary>What a render session should produce.</summary>
public sealed record RenderJob(int Width, int Height, int TargetSamples, TimeSpan? TimeLimit = null,
    bool Denoise = true, ImageFinish? Finish = null);

/// <summary>A displayable snapshot of a render in progress.</summary>
public sealed record RenderFrame(byte[] Bgra, int Width, int Height, int Samples, int TargetSamples, TimeSpan Elapsed,
    TimeSpan? Remaining, float ActiveFraction, bool IsFinal);

/// <summary>Summary of a finished, stopped or failed render.</summary>
public sealed record RenderResult(RenderFrame? LastFrame, int Samples, TimeSpan Elapsed, long Rays, int Triangles,
    bool Completed, bool Cancelled, bool StoppedForMemory = false)
{
    /// <summary>Millions of rays per second over the whole run.</summary>
    public double MegaRaysPerSecond => Elapsed.TotalSeconds > 0 ? Rays / Elapsed.TotalSeconds / 1e6 : 0;
}

/// <summary>
/// Runs a progressive render in the background and publishes frames for the interface. It builds the
/// acceleration structure, adds samples in batches sized to keep the preview updating about twice per second,
/// supports pause and cancel, and stops when the sample target, the time limit or full convergence is reached.
/// </summary>
public sealed class RenderSession : IDisposable
{
    private readonly RenderScene _scene;
    private readonly RenderCamera _camera;
    private readonly RenderSettings _settings;
    private readonly RenderJob _job;
    private readonly ManualResetEventSlim _running = new(true);
    private PathTracer? _tracer;

    public RenderSession(RenderScene scene, RenderCamera camera, RenderSettings settings, RenderJob job)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(job);
        if (job.TargetSamples < 1) throw new ArgumentOutOfRangeException(nameof(job), "At least one sample is required.");
        _scene = scene;
        _camera = camera;
        _settings = settings;
        _job = job;
    }

    /// <summary>Raised on a worker thread each time a new preview frame is ready. Handlers must copy what they keep.</summary>
    public event Action<RenderFrame>? FrameReady;

    /// <summary>Raised when the session changes phase, with a resource key such as "Render.Preparing".</summary>
    public event Action<string>? PhaseChanged;

    public bool IsPaused => !_running.IsSet;

    /// <summary>The tracer once preparation has finished; null before that.</summary>
    public PathTracer? Tracer => _tracer;

    /// <summary>Holds the render between passes until <see cref="Resume"/> is called.</summary>
    public void Pause() => _running.Reset();

    public void Resume() => _running.Set();

    /// <summary>Renders until the target is reached or <paramref name="cancellation"/> fires.</summary>
    public Task<RenderResult> RunAsync(CancellationToken cancellation) => Task.Run(() => RunGuarded(cancellation));

    /// <summary>
    /// Runs the render under a memory watchdog. If memory runs short the render stops where it is and the image so far is
    /// returned, flagged with <see cref="RenderResult.StoppedForMemory"/>; running out of memory is handled the same way.
    /// </summary>
    private RenderResult RunGuarded(CancellationToken cancellation)
    {
        using var safety = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var memoryStop = false;
        using var watchdog = new RenderSafety.Watchdog(
            (long)(RenderSafety.ReadMemory().TotalBytes * 0.8), () =>
            {
                memoryStop = true;
                safety.Cancel();
            });
        try
        {
            var result = Run(safety.Token);
            return memoryStop ? result with { StoppedForMemory = true, Completed = false } : result;
        }
        catch (OutOfMemoryException)
        {
            // Release what the tracer holds before reporting, so the interface has memory to show the message.
            var kept = _lastFrame;
            _tracer = null;
            GC.Collect();
            return new RenderResult(kept, kept?.Samples ?? 0, TimeSpan.Zero, 0, _scene.TriangleCount, false, true, true);
        }
    }

    private RenderFrame? _lastFrame;

    private RenderResult Run(CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        RenderFrame? last = null;
        try
        {
            PhaseChanged?.Invoke("Render.Preparing");
            var settings = _settings with
            {
                // Render settings carry the sample cap implicitly; the job decides the pace.
                MaxThreads = _settings.MaxThreads
            };
            var tracer = _tracer = new PathTracer(_scene, _job.Width, _job.Height, _camera, settings, cancellation);
            MemoryTrim.AfterHeavyBuild();
            PhaseChanged?.Invoke("Render.Rendering");
            clock.Restart();

            var finish = _job.Finish ?? new ImageFinish();
            var lastFrameAt = TimeSpan.Zero;
            var frameCost = TimeSpan.FromMilliseconds(50);
            var batch = 1;
            var converged = false;
            while (tracer.SampleCount < _job.TargetSamples && !converged)
            {
                cancellation.ThrowIfCancellationRequested();
                _running.Wait(cancellation);
                var batchStart = clock.Elapsed;
                var passes = Math.Min(batch, _job.TargetSamples - tracer.SampleCount);
                for (var i = 0; i < passes; i++)
                {
                    tracer.RenderPass(cancellation);
                    if (_job.TimeLimit is { } limit && clock.Elapsed >= limit) break;
                }
                var passTime = (clock.Elapsed - batchStart) / Math.Max(1, passes);

                converged = settings.AdaptiveSampling && tracer.ActiveFraction <= 0.0005f &&
                            tracer.SampleCount >= settings.AdaptiveMinimumSamples;
                var finished = tracer.SampleCount >= _job.TargetSamples || converged ||
                               (_job.TimeLimit is { } timeLimit && clock.Elapsed >= timeLimit);

                // Refresh about twice per second, but never spend more than a quarter of the time producing frames.
                var interval = TimeSpan.FromMilliseconds(Math.Max(450, frameCost.TotalMilliseconds * 4));
                if (finished || last is null || clock.Elapsed - lastFrameAt >= interval)
                {
                    var frameStart = clock.Elapsed;
                    last = _lastFrame = MakeFrame(tracer, finish, clock.Elapsed, finished);
                    frameCost = clock.Elapsed - frameStart;
                    lastFrameAt = clock.Elapsed;
                    FrameReady?.Invoke(last);
                }
                if (finished) break;
                var wanted = interval.TotalSeconds / Math.Max(passTime.TotalSeconds, 1e-4);
                batch = (int)Math.Clamp(Math.Round(wanted * 0.6), 1, 64);
            }
            return new RenderResult(last, tracer.SampleCount, clock.Elapsed, tracer.RayCount, tracer.TriangleCount,
                true, false);
        }
        catch (OperationCanceledException)
        {
            var tracer = _tracer;
            // A stopped render still yields what it has so far, so the user can keep or save it.
            if (tracer is not null && tracer.SampleCount > 0)
            {
                last = MakeFrame(tracer, _job.Finish ?? new ImageFinish(), clock.Elapsed, isFinal: true);
                FrameReady?.Invoke(last);
            }
            return new RenderResult(last, tracer?.SampleCount ?? 0, clock.Elapsed, tracer?.RayCount ?? 0,
                tracer?.TriangleCount ?? _scene.TriangleCount, false, true);
        }
    }

    private RenderFrame MakeFrame(PathTracer tracer, ImageFinish finish, TimeSpan elapsed, bool isFinal)
    {
        var bytes = ImageProcessor.ToBgra32(tracer.LinearImage(_job.Denoise), tracer.Width, tracer.Height,
            tracer.BaseExposure, finish, tracer.CaptureFeatures);
        TimeSpan? remaining = null;
        if (tracer.SampleCount > 0 && !isFinal)
        {
            var perPass = elapsed / tracer.SampleCount;
            remaining = perPass * Math.Max(0, _job.TargetSamples - tracer.SampleCount);
            if (_job.TimeLimit is { } limit) remaining = TimeSpan.FromTicks(Math.Min(remaining.Value.Ticks, Math.Max(0, (limit - elapsed).Ticks)));
        }
        return new RenderFrame(bytes, tracer.Width, tracer.Height, tracer.SampleCount, _job.TargetSamples, elapsed,
            remaining, tracer.ActiveFraction, isFinal);
    }

    /// <summary>
    /// Re-applies the picture finish to the current image without rendering more, so exposure and grading sliders
    /// feel instant. Returns null before the first sample.
    /// </summary>
    public RenderFrame? Refinish(ImageFinish finish, TimeSpan elapsed)
    {
        var tracer = _tracer;
        return tracer is null || tracer.SampleCount == 0 ? null : MakeFrame(tracer, finish, elapsed, isFinal: true);
    }

    public void Dispose() => _running.Dispose();
}
