using System.Numerics;
using System.Windows;
using System.Windows.Input;
using Guanacaste3D.Core.Navigation;

namespace Guanacaste3D.Desktop;

internal sealed partial class SceneNavigator
{
    private bool _flightGesture;

    /// <summary>Explicit metres per second; null preserves project-scaled modeling navigation.</summary>
    public float? TravelSpeed { get; set; }

    /// <summary>Keeps forward travel level while allowing Q/E to change floors.</summary>
    public bool HorizontalTravel { get; set; }

    /// <summary>Lets wheel travel cross picked surfaces instead of approaching them asymptotically.</summary>
    public bool FreeTravel { get; set; }

    /// <summary>Groups continuous keyboard movement into one view-history entry and applies metric speed.</summary>
    private void ApplyFlight(Vector3 input, float seconds, bool fast, bool precise)
    {
        if (input == Vector3.Zero)
        {
            EndFlight();
            return;
        }
        if (!_flightGesture)
        {
            _history.Record(Camera);
            _flightGesture = true;
        }
        var speed = TravelSpeed ?? Math.Clamp(SceneRadius() * 0.25f, 1.5f, 50f);
        MoveCamera(CameraTravel.Displacement(Camera, input, speed, seconds, HorizontalTravel, fast, precise));
        Changed?.Invoke();
    }

    /// <summary>Commits flight history on release, focus loss, hiding, or camera locking.</summary>
    private void EndFlight()
    {
        if (!_flightGesture) return;
        _flightGesture = false;
        _history.Record(Camera);
    }
}
