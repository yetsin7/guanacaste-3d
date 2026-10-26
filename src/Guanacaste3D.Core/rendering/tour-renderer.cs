using System.Numerics;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Core.Rendering;

/// <summary>Progress of a video render.</summary>
public readonly record struct TourProgress(int Frame, int Total, double Seconds);

/// <summary>Outcome of a video render.</summary>
public sealed record TourResult(int FramesWritten, int TotalFrames, bool Cancelled, bool StoppedForMemory, string Folder);

/// <summary>
/// Renders a walk-through as numbered PNG frames. One path tracer, and so one acceleration structure, serves every
/// frame; only the camera changes between them, which makes a long video affordable. If memory runs short or the
/// user cancels, the frames already written are kept.
/// </summary>
public static class TourRenderer
{
    public static Task<TourResult> RenderAsync(RenderScene scene, CameraPath path, RenderSettings settings, int width, int height,
        int framesPerSecond, int samplesPerFrame, ImageFinish finish, string folder, IProgress<TourProgress>? progress,
        CancellationToken cancellation) =>
        Task.Run(() => Render(scene, path, settings, width, height, framesPerSecond, samplesPerFrame, finish, folder, progress, cancellation));

    private static TourResult Render(RenderScene scene, CameraPath path, RenderSettings settings, int width, int height,
        int framesPerSecond, int samplesPerFrame, ImageFinish finish, string folder, IProgress<TourProgress>? progress,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Marks.Count < 2) throw new InvalidOperationException("A tour needs at least two camera marks.");
        Directory.CreateDirectory(folder);
        var total = path.FrameCount(framesPerSecond);
        var written = 0;
        var memoryStop = false;

        using var safety = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using var watchdog = new RenderSafety.Watchdog((long)(RenderSafety.ReadMemory().TotalBytes * 0.8), () =>
        {
            memoryStop = true;
            safety.Cancel();
        });
        try
        {
            var first = path.Frame(0, framesPerSecond);
            var tracer = new PathTracer(scene, width, height, new RenderCamera(first.Position, first.Target, first.FieldOfView) with
            {
                CorrectVerticals = false
            }, settings with { AdaptiveSampling = true }, safety.Token);
            MemoryTrim.AfterHeavyBuild();
            for (var frame = 0; frame < total; frame++)
            {
                safety.Token.ThrowIfCancellationRequested();
                var (position, target, fieldOfView) = path.Frame(frame, framesPerSecond);
                tracer.SetCamera(new RenderCamera(position, target, fieldOfView));
                for (var sample = 0; sample < samplesPerFrame; sample++)
                {
                    tracer.RenderPass(safety.Token);
                    if (tracer.ActiveFraction <= 0.0005f && sample >= settings.AdaptiveMinimumSamples) break;
                }
                var bytes = ImageProcessor.ToBgra32(tracer.LinearImage(denoise: true), width, height, tracer.BaseExposure, finish,
                    tracer.CaptureFeatures);
                PngCodec.Save(Path.Combine(folder, $"frame_{frame + 1:00000}.png"), RasterImage.FromBgra32(width, height, bytes));
                written++;
                progress?.Report(new TourProgress(written, total, written / (double)Math.Max(1, framesPerSecond)));
            }
        }
        catch (OperationCanceledException)
        {
            return new TourResult(written, total, !memoryStop, memoryStop, folder);
        }
        catch (OutOfMemoryException)
        {
            return new TourResult(written, total, false, true, folder);
        }
        return new TourResult(written, total, false, false, folder);
    }
}
