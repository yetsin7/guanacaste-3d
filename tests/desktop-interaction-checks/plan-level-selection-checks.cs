using Guanacaste3D.Core;

internal static partial class InteractionChecks
{
    /// <summary>Ensures plan selection cannot include same-type elements on another level.</summary>
    private static void PlanLevelSelectionChecks(DocumentSession session)
    {
        var activeLevel = Field<Guid>("_activeLevelId");
        var upperLevel = session.AddLevel();
        var lowerWall = session.AddWall(activeLevel, new Point2(500, 500), new Point2(504, 500));
        var upperWall = session.AddWall(upperLevel, new Point2(500, 510), new Point2(504, 510));
        var door = session.AddOpening(lowerWall, OpeningKind.Door, 2);
        try
        {
            SetField("_selectedOpeningId", null);
            SetField("_selectedWallId", (Guid?)lowerWall);
            SetField("_viewSelection", null);
            Invoke("SelectSameTypeInView");
            var selection = Field<HashSet<Guid>>("_viewSelection");
            Check(selection.Contains(lowerWall) && !selection.Contains(upperWall),
                "Selecting matching instances in plan excludes walls on another level");
            Invoke("SelectAllInView");
            selection = Field<HashSet<Guid>>("_viewSelection");
            Check(selection.Contains(lowerWall) && selection.Contains(door) && !selection.Contains(upperWall),
                "Selecting all in plan includes visible openings only on the active level");
        }
        finally
        {
            SetField("_viewSelection", null);
            SetField("_selectedWallId", null);
            session.RemoveWall(lowerWall);
            session.RemoveWall(upperWall);
            session.RemoveLevel(upperLevel);
        }
    }
}
