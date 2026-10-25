using Guanacaste3D.Core;

/// <summary>Regression checks for the architectural plan symbol of hosted doors.</summary>
internal static class DoorPlanSymbolChecks
{
    /// <summary>Checks frame placement, leaf count, swing radius and the leaf's real thickness.</summary>
    public static void Run(Action<bool, string> check)
    {
        var single = DoorPlanSymbol.Build(3, 3.9, 0.2, 0.04);
        var frames = single.Outlines.Where(item => item.Part == DoorPlanPart.Frame).ToArray();
        check(frames.Length == 4 && frames.All(frame => frame.Points.All(point => point.U >= 3 - 1e-9 && point.U <= 3.9 + 1e-9 &&
            Math.Abs(point.V) < 0.1)), "Door jamb linings stay inside the rough opening and inside both wall faces");
        check(single.Outlines.Count(item => item.Part == DoorPlanPart.Casing) == 4 &&
            single.Outlines.Where(item => item.Part == DoorPlanPart.Casing).All(casing => casing.Points.All(point => Math.Abs(point.V) >= 0.1 - 1e-9)),
            "Door casings sit proud of both wall faces, one per jamb and face");
        var leaf = single.Outlines.Single(item => item.Part == DoorPlanPart.Leaf);
        var leafLength = leaf.Points.Max(point => point.V) - leaf.Points.Min(point => point.V);
        var leafThickness = leaf.Points.Max(point => point.U) - leaf.Points.Min(point => point.U);
        var swing = single.Swings.Single();
        check(Math.Abs(leafLength - swing.Radius) < 1e-9 && Math.Abs(leafThickness - 0.04) < 1e-9,
            "Single door leaf is drawn open at 90° with its real thickness and a matching swing radius");
        check(Math.Abs(swing.Radius - (0.9 - 2 * 0.06 - 2 * 0.004)) < 1e-9,
            "Swing radius equals the clear leaf width between linings");
        var arcEnd = DoorPlanSymbol.Sample(swing).Last();
        check(Math.Abs(arcEnd.U - swing.CenterU) < 1e-9 && Math.Abs(arcEnd.V - (swing.CenterV + swing.Radius)) < 1e-9,
            "Swing arc ends exactly at the open leaf tip");

        var pair = DoorPlanSymbol.Build(0, 1.5, 0.2, 0.04);
        var leaves = pair.Outlines.Where(item => item.Part == DoorPlanPart.Leaf).ToArray();
        check(leaves.Length == 2 && pair.Swings.Count == 2, "Openings wider than 1.2 m are drawn as a pair of leaves");
        // The begin leaf closes at angle 0 (its first sample); the end leaf closes at angle π (its last sample).
        var closedBegin = DoorPlanSymbol.Sample(pair.Swings[0]).First();
        var closedEnd = DoorPlanSymbol.Sample(pair.Swings[1]).Last();
        check(Math.Abs(closedBegin.U - closedEnd.U) < 1e-9 && Math.Abs(closedBegin.U - 0.75) < 1e-9,
            "Both swings of a pair close on the same meeting line at the centre of the opening");
    }
}
