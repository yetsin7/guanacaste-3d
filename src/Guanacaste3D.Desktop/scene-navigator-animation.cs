using System.Numerics;
using System.Windows.Media;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop;

internal sealed partial class SceneNavigator
{
    private const double TransitionSeconds = 0.38;
    private bool _transitionRunning;
    private NavigationCamera? _transitionStart;
    private NavigationCamera? _transitionEnd;
    private NavigationCamera? _transitionLast;
    private DateTime _transitionBegan;

    /// <summary>True while a cube-driven camera glide is still in progress.</summary>
    public bool IsTransitioning => _transitionRunning;

    /// <summary>
    /// Glides to the orientation <see cref="LookFrom"/> would snap to. The final pose is exactly the snapped pose, so
    /// history, framing and tests behave identically; only the path between the two poses is animated.
    /// </summary>
    public void LookFromAnimated(Vector3 direction)
    {
        CancelTransition();
        var start = Camera.Clone();
        LookFrom(direction);
        var end = Camera.Clone();
        if (SamePose(start, end)) return;
        ApplyPose(start);
        NotifyChanged();
        _transitionStart = start;
        _transitionEnd = end;
        _transitionLast = start;
        _transitionBegan = DateTime.UtcNow;
        _transitionRunning = true;
        CompositionTarget.Rendering += TransitionFrame;
    }

    /// <summary>Stops a glide in place, leaving the camera wherever it currently is.</summary>
    public void CancelTransition()
    {
        if (!_transitionRunning) return;
        CompositionTarget.Rendering -= TransitionFrame;
        _transitionRunning = false;
    }

    /// <summary>Advances the glide, yielding immediately when the user moves the camera by any other means.</summary>
    private void TransitionFrame(object? sender, EventArgs e)
    {
        if (!_transitionRunning || _transitionStart is null || _transitionEnd is null || _transitionLast is null) { CancelTransition(); return; }
        if (!SamePose(Camera, _transitionLast)) { CancelTransition(); return; }
        var progress = Math.Clamp((DateTime.UtcNow - _transitionBegan).TotalSeconds / TransitionSeconds, 0, 1);
        if (progress >= 1)
        {
            ApplyPose(_transitionEnd);
            CancelTransition();
            NotifyChanged();
            return;
        }
        var eased = (float)(progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.Pow(-2 * progress + 2, 3) / 2);
        var (min, max) = Bounds();
        var center = (min + max) / 2;
        var pose = Interpolate(_transitionStart, _transitionEnd, center, eased);
        ApplyPose(pose);
        _transitionLast = pose;
        NotifyChanged();
    }

    /// <summary>Arcs the eye around the model center, so the camera never cuts through the building mid-glide.</summary>
    private static NavigationCamera Interpolate(NavigationCamera from, NavigationCamera to, Vector3 center, float t)
    {
        var a = from.Position - center;
        var b = to.Position - center;
        var lengthA = MathF.Max(a.Length(), 1e-3f);
        var lengthB = MathF.Max(b.Length(), 1e-3f);
        var unitA = a / lengthA;
        var unitB = b / lengthB;
        var dot = Math.Clamp(Vector3.Dot(unitA, unitB), -1f, 1f);
        var angle = MathF.Acos(dot);
        Vector3 direction;
        if (angle < 1e-3f) direction = Vector3.Normalize(Vector3.Lerp(unitA, unitB, t));
        else if (MathF.PI - angle < 1e-3f)
        {
            // Opposite sides: sweep over the top so the arc is well defined.
            var axis = Vector3.Normalize(Vector3.Cross(unitA, Vector3.UnitZ) is { } cross && cross.LengthSquared() > 1e-6f
                ? cross : Vector3.UnitX);
            direction = Vector3.Transform(unitA, Quaternion.CreateFromAxisAngle(axis, -MathF.PI * t));
        }
        else
        {
            var sin = MathF.Sin(angle);
            direction = unitA * (MathF.Sin((1 - t) * angle) / sin) + unitB * (MathF.Sin(t * angle) / sin);
        }
        var yawDelta = ((to.YawDegrees - from.YawDegrees + 540) % 360) - 180;
        return new NavigationCamera
        {
            Position = center + direction * (lengthA + (lengthB - lengthA) * t),
            YawDegrees = (from.YawDegrees + yawDelta * t + 360) % 360,
            PitchDegrees = from.PitchDegrees + (to.PitchDegrees - from.PitchDegrees) * t,
            FieldOfViewDegrees = to.FieldOfViewDegrees,
            FramingTarget = to.FramingTarget
        };
    }

    /// <summary>Copies a pose into the live camera without replacing the shared instance.</summary>
    private void ApplyPose(NavigationCamera pose)
    {
        Camera.Position = pose.Position;
        Camera.YawDegrees = pose.YawDegrees;
        Camera.PitchDegrees = pose.PitchDegrees;
        Camera.FieldOfViewDegrees = pose.FieldOfViewDegrees;
        Camera.FramingTarget = pose.FramingTarget;
    }

    /// <summary>Compares two poses within a tolerance far below one pixel of visible motion.</summary>
    private static bool SamePose(NavigationCamera a, NavigationCamera b) =>
        Vector3.DistanceSquared(a.Position, b.Position) < 1e-8f && MathF.Abs(a.YawDegrees - b.YawDegrees) < 1e-4f &&
        MathF.Abs(a.PitchDegrees - b.PitchDegrees) < 1e-4f;
}
