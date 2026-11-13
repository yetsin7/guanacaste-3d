using System.Numerics;
using System.Windows;

namespace Guanacaste3D.Desktop;

internal sealed partial class SceneNavigator
{
    /// <summary>Returns the scale plane and pointer offset used by the orthographic viewport.</summary>
    private (Vector3 Offset, float Depth) OrthographicFocus(Point point)
    {
        var (width, height) = Size();
        var (min, max) = Bounds();
        var target = Camera.FramingTarget ?? (min + max) / 2;
        var depth = MathF.Max(0.05f, Vector3.Dot(target - Camera.Position, Camera.Forward));
        var ray = Camera.OrthographicRay((float)point.X, (float)point.Y, width, height, target, 0);
        return (ray.Origin - Camera.Position, depth);
    }

    /// <summary>Changes parallel scale around a screen point while keeping that point under the pointer.</summary>
    private float ZoomOrthographic(Point point, float fraction)
    {
        var (offset, depth) = OrthographicFocus(point);
        fraction = Math.Clamp(fraction, MathF.Min(1, 0.05f / depth), 100);
        MoveCamera((offset + Camera.Forward * depth) * (1 - fraction));
        return fraction;
    }
}
