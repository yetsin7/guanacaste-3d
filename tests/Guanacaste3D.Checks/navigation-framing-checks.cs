using System.Numerics;
using Guanacaste3D.Core.Navigation;

/// <summary>Checks orthographic framing and its independent navigation history.</summary>
static class NavigationFramingChecks
{
    /// <summary>Exercises a small selection far from the project center and restores its framing plane.</summary>
    public static void Run(Action<bool, string> check)
    {
        var camera = new NavigationCamera { YawDegrees = 180, PitchDegrees = 0, FieldOfViewDegrees = 45 };
        var minimum = new Vector3(99.9f, -0.1f, -0.1f);
        var maximum = new Vector3(100.1f, 0.1f, 0.1f);
        var history = new CameraViewHistory();
        history.Record(camera);
        camera.FrameBounds(minimum, maximum, 1);
        var width = camera.OrthographicWidth(Vector3.Zero, 1);
        check(width >= 0.2f && width < 1, "orthographic framing: distant small selection determines the scale");
        var centerPixel = camera.ProjectOrthographic(new Vector3(100, 0, 0), 800, 800, Vector3.Zero);
        var nearPixel = camera.ProjectOrthographic(new Vector3(99, 0.05f, 0.05f), 800, 800, Vector3.Zero);
        var farPixel = camera.ProjectOrthographic(new Vector3(101, 0.05f, 0.05f), 800, 800, Vector3.Zero);
        check(Math.Abs(centerPixel.X - 400) < 0.01f && Math.Abs(centerPixel.Y - 400) < 0.01f &&
              Vector2.Distance(new Vector2(nearPixel.X, nearPixel.Y), new Vector2(farPixel.X, farPixel.Y)) < 0.01f,
            "orthographic projection: points at different depths share the same screen position");
        var ray = camera.OrthographicRay(nearPixel.X, nearPixel.Y, 800, 800, Vector3.Zero, 500);
        var rayOffset = new Vector3(99, 0.05f, 0.05f) - ray.Origin;
        check(Vector3.Cross(rayOffset, ray.Direction).Length() < 1e-4f &&
              Vector3.Dot(rayOffset, ray.Direction) > 0,
            "orthographic picking: the screen point produces a parallel ray through the model point");
        history.Record(camera);
        camera.FrameBounds(new Vector3(-10), new Vector3(10), 1);
        history.Record(camera);
        var restored = history.Restore(camera, false);
        check(restored && camera.FramingTarget == new Vector3(100, 0, 0) &&
              Math.Abs(camera.OrthographicWidth(Vector3.Zero, 1) - width) < 1e-5f,
            "orthographic framing: navigation history restores the selection scale plane");
        check(history.Restore(camera, false) && camera.FramingTarget is null,
            "orthographic framing: history restores the original project fallback");
    }
}
