using System.Numerics;
using Guanacaste3D.Core.Geometry;

namespace Guanacaste3D.Core;

/// <summary>
/// The solids of the plumbing: every routed pipe as a round tube of its outside diameter, coloured by system as in
/// Revit's system graphics (blue cold, red hot, PVC drain), an angle stop where each supply riser meets its fixture,
/// the meter box and the outlet's cleanout cap.
/// </summary>
public static class PlumbingGeometry
{
    public static void AppendTo(SceneMesh mesh, BuildingDocument document, PlumbingAnalysis analysis)
    {
        foreach (var report in analysis.Systems)
        {
            var kind = Kind(report.System.Kind);
            foreach (var run in report.Runs)
                Tube(mesh, new Vector3((float)run.From.X, (float)run.From.Y, (float)run.FromZ),
                    new Vector3((float)run.To.X, (float)run.To.Y, (float)run.ToZ), (float)(run.OutsideMillimeters / 2000), kind);
            if (report.System.Kind == PlumbingSystemKind.Sanitary) continue;
            // An angle stop (llave de paso) at the top of each riser.
            foreach (var run in report.Runs.Where(run => run.From.DistanceTo(run.To) < 1e-9 && run.ToZ > run.FromZ))
            {
                var top = new Vector3((float)run.To.X, (float)run.To.Y, (float)run.ToZ);
                Tube(mesh, top - new Vector3(0, 0, 0.02f), top + new Vector3(0, 0, 0.03f), 0.016f, SurfaceKind.Metal);
            }
        }
        var elevations = document.Levels.ToDictionary(level => level.Id, level => level.Elevation);
        foreach (var point in document.PlumbingPoints)
        {
            if (!elevations.TryGetValue(point.LevelId, out var level)) continue;
            var at = new Vector3((float)point.Position.X, (float)point.Position.Y, (float)level);
            if (point.Kind == PlumbingPointKind.WaterMeter)
            {
                // A meter box flush with the ground, its lid slightly raised.
                Tube(mesh, at - new Vector3(0, 0, 0.25f), at + new Vector3(0, 0, 0.015f), 0.17f, SurfaceKind.Structure);
                Tube(mesh, at + new Vector3(0, 0, 0.015f), at + new Vector3(0, 0, 0.025f), 0.15f, SurfaceKind.Metal);
            }
            else
            {
                Tube(mesh, at - new Vector3(0, 0, 0.02f), at + new Vector3(0, 0, 0.012f), 0.07f, SurfaceKind.PipeDrain);
            }
        }
    }

    private static SurfaceKind Kind(PlumbingSystemKind kind) => kind switch
    {
        PlumbingSystemKind.ColdWater => SurfaceKind.PipeCold,
        PlumbingSystemKind.HotWater => SurfaceKind.PipeHot,
        _ => SurfaceKind.PipeDrain
    };

    /// <summary>An eight-sided tube with closed ends from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static void Tube(SceneMesh mesh, Vector3 a, Vector3 b, float radius, SurfaceKind kind)
    {
        var axis = b - a;
        var length = axis.Length();
        if (length < 1e-5f || radius <= 0) return;
        var direction = axis / length;
        var helper = MathF.Abs(direction.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX;
        var u = Vector3.Normalize(Vector3.Cross(helper, direction));
        // (u, v, direction) is right-handed, so the rings below run counter-clockwise seen from b and the quads face out.
        var v = Vector3.Cross(direction, u);
        const int sides = 8;
        var ringA = new Vector3[sides];
        var ringB = new Vector3[sides];
        for (var index = 0; index < sides; index++)
        {
            var angle = index * MathF.Tau / sides;
            var offset = (u * MathF.Cos(angle) + v * MathF.Sin(angle)) * radius;
            ringA[index] = a + offset;
            ringB[index] = b + offset;
        }
        for (var index = 0; index < sides; index++)
        {
            var next = (index + 1) % sides;
            mesh.AddQuad(ringA[index], ringA[next], ringB[next], ringB[index], kind);
        }
        for (var index = 1; index + 1 < sides; index++)
        {
            mesh.AddTriangle(ringB[0], ringB[index], ringB[index + 1], kind);
            mesh.AddTriangle(ringA[0], ringA[index + 1], ringA[index], kind);
        }
    }
}
