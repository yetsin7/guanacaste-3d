using System.Numerics;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>
/// The solids of electrical devices: wall plates with their slots or rocker, the panel box, and luminaires whose diffuser
/// glows (<see cref="SurfaceKind.LightEmitter"/>) when the simulation says they are lit and is plain white
/// (<see cref="SurfaceKind.LightOff"/>) when they are not.
/// </summary>
public static class ElectricalGeometry
{
    /// <summary>Appends one device standing on a level at <paramref name="datum"/>, lit to <paramref name="brightness"/>.</summary>
    public static void AppendTo(SceneMesh mesh, ElectricalDevice device, double datum, double brightness)
    {
        var position = new Vector3((float)device.Position.X, (float)device.Position.Y, (float)(datum + device.Elevation));
        var outward = new Vector3(MathF.Cos((float)device.Facing), MathF.Sin((float)device.Facing), 0);
        var along = new Vector3(-outward.Y, outward.X, 0);
        var glow = brightness > 0 ? SurfaceKind.LightEmitter : SurfaceKind.LightOff;
        switch (device.Kind)
        {
            case ElectricalDeviceKind.Outlet or ElectricalDeviceKind.GfciOutlet or ElectricalDeviceKind.UsbOutlet:
                Box(mesh, position, along, outward, 0.0375f, 0.004f, 0.004f, -0.06f, 0.06f, SurfaceKind.Ceramic);
                foreach (var row in new[] { -0.025f, 0.025f })
                {
                    // Two outlets per plate, each with two slots and the ground hole.
                    foreach (var slot in new[] { -0.007f, 0.007f })
                        Box(mesh, position + along * slot, along, outward, 0.0012f, 0.0015f, 0.009f, row - 0.006f, row + 0.006f, SurfaceKind.OpeningFrame);
                    Box(mesh, position, along, outward, 0.002f, 0.0015f, 0.009f, row - 0.016f, row - 0.012f, SurfaceKind.OpeningFrame);
                }
                if (device.Kind == ElectricalDeviceKind.GfciOutlet)
                    Box(mesh, position, along, outward, 0.012f, 0.002f, 0.0095f, -0.004f, 0.004f, SurfaceKind.Metal);
                break;
            case ElectricalDeviceKind.Outlet240:
                Box(mesh, position, along, outward, 0.06f, 0.005f, 0.005f, -0.06f, 0.06f, SurfaceKind.Ceramic);
                Box(mesh, position, along, outward, 0.025f, 0.0015f, 0.011f, -0.025f, 0.025f, SurfaceKind.OpeningFrame);
                break;
            case ElectricalDeviceKind.Switch or ElectricalDeviceKind.ThreeWaySwitch or ElectricalDeviceKind.Dimmer:
            {
                Box(mesh, position, along, outward, 0.0375f, 0.004f, 0.004f, -0.06f, 0.06f, SurfaceKind.Ceramic);
                // The rocker shows its position: pressed at the top when on.
                var tilt = device.IsOn ? 0.004f : -0.004f;
                Box(mesh, position, along, outward, 0.016f, 0.008f, 0.012f + tilt, 0, 0.03f, SurfaceKind.Ceramic);
                Box(mesh, position, along, outward, 0.016f, 0.008f, 0.012f - tilt, -0.03f, 0, SurfaceKind.Ceramic);
                if (device.Kind == ElectricalDeviceKind.Dimmer)
                    Box(mesh, position, along, outward, 0.004f, 0.012f, 0.014f, -0.045f, -0.035f, SurfaceKind.Metal);
                break;
            }
            case ElectricalDeviceKind.Panel:
                Box(mesh, position, along, outward, 0.21f, 0.05f, 0.05f, -0.30f, 0.30f, SurfaceKind.Metal);
                Box(mesh, position, along, outward, 0.16f, 0.004f, 0.103f, -0.24f, 0.20f, SurfaceKind.OpeningFrame);
                Box(mesh, position + along * 0.15f, along, outward, 0.01f, 0.006f, 0.106f, -0.05f, 0.05f, SurfaceKind.Metal);
                break;
            case ElectricalDeviceKind.CeilingFixture:
                Disc(mesh, position, 0.20f, -0.025f, 0, SurfaceKind.Metal);
                Disc(mesh, position, 0.17f, -0.09f, -0.025f, glow);
                break;
            case ElectricalDeviceKind.Downlight:
                Disc(mesh, position, 0.075f, -0.008f, 0.004f, SurfaceKind.Metal);
                Disc(mesh, position, 0.055f, -0.010f, -0.006f, brightness > 0 ? SurfaceKind.SpotEmitter : SurfaceKind.LightOff);
                break;
            case ElectricalDeviceKind.WallSconce:
                Box(mesh, position, along, outward, 0.05f, 0.012f, 0.012f, -0.08f, 0.08f, SurfaceKind.Metal);
                Box(mesh, position + outward * 0.06f, along, outward, 0.07f, 0.05f, 0.05f, -0.12f, 0.12f, glow);
                break;
            case ElectricalDeviceKind.CeilingFan:
            {
                // Canopy, a 30 cm downrod, the motor housing, five blades on their irons, and the light kit under it.
                Disc(mesh, position, 0.07f, -0.05f, 0f, SurfaceKind.Metal);
                Disc(mesh, position, 0.012f, -0.32f, -0.05f, SurfaceKind.Metal);
                Disc(mesh, position, 0.10f, -0.44f, -0.32f, SurfaceKind.Metal);
                for (var blade = 0; blade < 5; blade++)
                {
                    var angle = blade * MathF.Tau / 5 + 0.3f;
                    var radial = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0);
                    var tangent = new Vector3(-radial.Y, radial.X, 0);
                    Box(mesh, position, tangent, radial, 0.012f, 0.06f, 0.14f, -0.42f, -0.41f, SurfaceKind.Metal);
                    Box(mesh, position, tangent, radial, 0.065f, 0.27f, 0.42f, -0.425f, -0.415f, SurfaceKind.DoorLeaf);
                }
                Disc(mesh, position, 0.09f, -0.50f, -0.44f, glow);
                break;
            }
            case ElectricalDeviceKind.Pendant:
                Disc(mesh, position, 0.05f, -0.02f, 0, SurfaceKind.Metal);
                Disc(mesh, position, 0.004f, -0.70f, -0.02f, SurfaceKind.OpeningFrame);
                Disc(mesh, position, 0.17f, -0.95f, -0.70f, SurfaceKind.OpeningFrame);
                Disc(mesh, position, 0.15f, -0.96f, -0.93f, glow);
                break;
        }
    }

    /// <summary>A box centred on <paramref name="origin"/> along the wall, offset out from it, between two heights.</summary>
    private static void Box(SceneMesh mesh, Vector3 origin, Vector3 along, Vector3 outward, float halfAlong, float halfOut,
        float offsetOut, float bottom, float top, SurfaceKind kind)
    {
        var center = origin + outward * offsetOut;
        Vector3 P(float a, float o, float z) => center + along * a + outward * o + new Vector3(0, 0, z);
        var corners = new[] { P(-halfAlong, -halfOut, 0), P(halfAlong, -halfOut, 0), P(halfAlong, halfOut, 0), P(-halfAlong, halfOut, 0) };
        Prism(mesh, corners, bottom, top, kind);
    }

    /// <summary>A twelve-sided disc or cylinder around a vertical axis.</summary>
    private static void Disc(SceneMesh mesh, Vector3 center, float radius, float bottom, float top, SurfaceKind kind)
    {
        const int sides = 16;
        var ring = Enumerable.Range(0, sides).Select(index => center + new Vector3(
            radius * MathF.Cos(index * MathF.Tau / sides), radius * MathF.Sin(index * MathF.Tau / sides), 0)).ToArray();
        Prism(mesh, ring, bottom, top, kind);
    }

    /// <summary>A vertical prism over a counter-clockwise (or clockwise) outline, between two heights relative to its points.</summary>
    private static void Prism(SceneMesh mesh, Vector3[] outline, float bottom, float top, SurfaceKind kind)
    {
        var area = 0f;
        for (var index = 0; index < outline.Length; index++)
        {
            var a = outline[index];
            var b = outline[(index + 1) % outline.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        if (area < 0) Array.Reverse(outline);
        var up = new Vector3(0, 0, top);
        var down = new Vector3(0, 0, bottom);
        for (var index = 1; index + 1 < outline.Length; index++)
        {
            mesh.AddTriangle(outline[0] + up, outline[index] + up, outline[index + 1] + up, kind);
            mesh.AddTriangle(outline[index + 1] + down, outline[index] + down, outline[0] + down, kind);
        }
        for (var index = 0; index < outline.Length; index++)
        {
            var a = outline[index];
            var b = outline[(index + 1) % outline.Length];
            mesh.AddQuad(a + down, b + down, b + up, a + up, kind);
        }
    }
}
