using Guanacaste3D.Core;

/// <summary>Regression checks for mitered wall corners, joined moves, chained corners, and plan guides.</summary>
static class WallJoinChecks
{
    /// <summary>Verifies corner miters, flat ends where walls do not form a simple corner, and guide visibility.</summary>
    public static void Run(Action<bool, string> check)
    {
        var levelId = Guid.NewGuid();
        var first = new Wall(Guid.NewGuid(), levelId, new Point2(0, 0), new Point2(4, 0), 0.2, 3);
        var second = new Wall(Guid.NewGuid(), levelId, new Point2(4, 0), new Point2(4, 3), 0.2, 3);

        var joins = WallJoinIndex.Build([first, second]);
        var firstFootprint = joins.Footprint(first);
        var secondFootprint = joins.Footprint(second);
        check(Near(firstFootprint.EndLeft, new Point2(3.9, 0.1)) && Near(firstFootprint.EndRight, new Point2(4.1, -0.1)),
            "Mitered corner puts inner and outer faces on the bisector of the two walls");
        check(Near(secondFootprint.StartLeft, firstFootprint.EndLeft) &&
              Near(secondFootprint.StartRight, firstFootprint.EndRight),
            "Joined walls share the same corner points, so no notch remains");

        var square = WallJoinIndex.Build([first]).Footprint(first);
        check(Near(square.EndLeft, new Point2(4, 0.1)) && Near(square.EndRight, new Point2(4, -0.1)),
            "Unjoined wall ends stay square to the centerline");

        var tee = new Wall(Guid.NewGuid(), levelId, new Point2(4, 0), new Point2(8, 0), 0.2, 3);
        var teeFootprint = WallJoinIndex.Build([first, second, tee]).Footprint(first);
        check(Near(teeFootprint.EndLeft, new Point2(4, 0.1)), "Three walls meeting at one point keep flat ends");

        var otherType = new Wall(Guid.NewGuid(), levelId, new Point2(4, 0), new Point2(4, 3), 0.2, 3)
        {
            WallTypeId = Guid.NewGuid()
        };
        var differentTypeFootprint = WallJoinIndex.Build([first, otherType]).Footprint(first);
        check(Near(differentTypeFootprint.EndLeft, new Point2(4, 0.1)) &&
              Near(differentTypeFootprint.EndRight, new Point2(4, -0.1)),
            "Walls of different types keep square ends at their shared corner");

        var outline = joins.SectionOutline(first, 0, first.Length);
        check(Near(outline[1], new Point2(3.9, 0.1)) && Near(outline[2], new Point2(4.1, -0.1)),
            "Mesh outline of a joined end uses the same miter as the plan");

        var crossing = WallPlacementGeometry.CenterlineIntersection(
            new WallCenterline(new Point2(0, 0.1), new Point2(4, 0.1)),
            new WallCenterline(new Point2(4.1, 0), new Point2(4.1, -3)));
        check(crossing is Point2 meeting && Near(meeting, new Point2(4.1, 0.1)),
            "Offset chain walls meet where their offset lines cross");
        check(WallPlacementGeometry.CenterlineIntersection(
                new WallCenterline(new Point2(0, 0), new Point2(1, 0)),
                new WallCenterline(new Point2(0, 1), new Point2(2, 1))) is null,
            "Parallel offset lines produce no corner");

        var document = BuildingDocument.Create();
        var session = new DocumentSession(document);
        var level = document.Levels[0].Id;
        var firstId = session.AddWall(level, new Point2(0, 0), new Point2(4, 0));
        var secondId = session.AddWall(level, new Point2(4, 0), new Point2(4, 3));
        session.MoveWall(firstId, 0, 1);
        var moved = session.Document.Walls.Single(wall => wall.Id == firstId);
        var neighbour = session.Document.Walls.Single(wall => wall.Id == secondId);
        check(moved.Start == new Point2(0, 1) && moved.End == new Point2(4, 1) &&
              neighbour.Start == new Point2(4, 1) && neighbour.End == new Point2(4, 3),
            "Moving a wall carries the coincident corner of its joined neighbour");
        session.Undo();
        check(session.Document.Walls.Single(wall => wall.Id == secondId).Start == new Point2(4, 0),
            "Undo restores the joined neighbour corner");

        var visible = new PlanBounds(-10, -10, 10, 10);
        var guides = PlanAlignment.Find(new Point2(4.02, 3), [new Point2(4, 0), new Point2(20, 3)], visible, 0.05);
        check(guides.Count == 1 && guides[0].Axis == PlanGuideAxis.Vertical && guides[0].Corner == new Point2(4, 0),
            "Reference line appears only for a visible corner on the pointer's axis");
        var offScreen = PlanAlignment.Find(new Point2(4.02, 3), [new Point2(4, 0)],
            new PlanBounds(5, -10, 10, 10), 0.05);
        check(offScreen.Count == 0, "Reference line stays hidden when the aligned corner is off screen");
    }

    /// <summary>Compares two plan points with a tolerance far below any drawing precision.</summary>
    private static bool Near(Point2 first, Point2 second) => first.DistanceTo(second) < 1e-9;
}
