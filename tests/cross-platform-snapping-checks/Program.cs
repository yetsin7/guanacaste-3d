using Guanacaste3D.Core;
using Guanacaste3D.CrossPlatform;

static void Verify(bool result, string message)
{
    if (!result) throw new Exception("FAILED: " + message);
    Console.WriteLine("OK: " + message);
}

var session = new DocumentSession(BuildingDocument.Create("Geometric drafting snaps"));
var firstLevel = session.Document.Levels[0].Id;
var secondLevel = session.Document.Levels[1].Id;
var wallId = session.AddWall(firstLevel, new Point2(1, 2), new Point2(5, 2));
var axisId = session.AddGridAxis("A", new Point2(0, 4), new Point2(10, 4));
var doc = session.Document;

var endpoint = PlanSnapping.Resolve(doc, firstLevel, new Point2(1.08, 2.03),
    80, useGrid: true, useEndpoints: true);
Verify(endpoint.Kind == PlanSnapping.SnapKind.Endpoint && endpoint.Point == new Point2(1, 2),
    "Endpoint takes priority over metric grid");

var otherFloor = PlanSnapping.Resolve(doc, secondLevel, new Point2(1.08, 2.03),
    80, useGrid: false, useEndpoints: true);
Verify(otherFloor.Kind == PlanSnapping.SnapKind.None,
    "Walls on an inactive level cannot attract drafting snaps");

var axis = PlanSnapping.Resolve(doc, secondLevel, new Point2(0.03, 4.04),
    80, useGrid: false, useEndpoints: true);
Verify(axis.Kind == PlanSnapping.SnapKind.Endpoint && axis.Point == new Point2(0, 4),
    "Grid axis endpoints are visible as cross-level references");

var grid = PlanSnapping.Resolve(doc, firstLevel, new Point2(3.29, 2.26),
    50, useGrid: true, useEndpoints: false);
Verify(grid.Kind == PlanSnapping.SnapKind.Grid && grid.Point == new Point2(3.25, 2.25),
    "Quarter-meter grid snap resolves correctly");

var noGrid = PlanSnapping.Resolve(doc, firstLevel, new Point2(3.29, 2.26),
    50, useGrid: false, useEndpoints: false);
Verify(noGrid.Kind == PlanSnapping.SnapKind.None && noGrid.Point == new Point2(3.29, 2.26),
    "Disabled snapping preserves pointer coordinates");

var shift = PlanSnapping.Resolve(doc, firstLevel, new Point2(4.11, 3.1),
    100, useGrid: false, useEndpoints: true, firstPoint: new Point2(1, 2), orthogonal: true);
Verify(shift.Kind == PlanSnapping.SnapKind.Orthogonal && shift.Point.Y == 2,
    "Shift draws strictly horizontal and does not snap to off-axis endpoints");

var constrainedGrid = PlanSnapping.Resolve(doc, firstLevel, new Point2(4.09, 2.01),
    100, useGrid: true, useEndpoints: false, firstPoint: new Point2(1, 2.1), orthogonal: true);
Verify(constrainedGrid.Point.Y == 2.1,
    "Grid snapping preserves the fixed coordinate of an orthogonal reference");

Verify(doc.Walls.Single(w => w.Id == wallId).Start == new Point2(1, 2) &&
       doc.Grids.Single(g => g.Id == axisId).Start == new Point2(0, 4),
    "Snapping never mutates walls or project datum geometry");
Console.WriteLine("Cross-platform drafting snapping regression checks passed.");
