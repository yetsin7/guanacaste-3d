using System.Text.Json.Nodes;
using Guanacaste3D.Agent;
using Guanacaste3D.Core;

/// <summary>Checks automatic element tags and automatic dimensions, including their assistant tools.</summary>
static class AnnotationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var document = ModernHouseShowcase.Create();
        var ground = document.Levels.Single(level => level.Role == LevelRole.BaseFloor).Id;
        var tags = ElementTags.Build(document, ground);
        var doors = tags.Where(tag => tag.Category == TagCategory.Door).ToList();
        check(doors.Count == 1 && doors[0].Mark == "P1", "The villa's single ground-floor door is tagged P1");

        var demo = SampleProjects.CreateDemonstrationHouse();
        var demoLevel = demo.Levels[0].Id;
        var demoTags = ElementTags.Build(demo, demoLevel);
        var demoDoorTypes = demo.Openings.Where(o => o.Kind == OpeningKind.Door).Select(o => o.OpeningTypeId).Distinct().Count();
        check(demoTags.Count(tag => tag.Category == TagCategory.Window) == demo.Openings.Count(o => o.Kind == OpeningKind.Window) &&
            demoTags.Where(tag => tag.Category == TagCategory.Door).Select(tag => tag.Mark).Distinct().Count() == demoDoorTypes,
            "Every window is tagged and openings of one type share a mark");

        var wall = demo.Walls.First(w => demo.Openings.Any(o => o.WallId == w.Id));
        var opening = demo.Openings.First(o => o.WallId == wall.Id);
        var tag = demoTags.Single(t => t.ElementId == opening.Id);
        var onAxis = new Point2(wall.Start.X + (wall.End.X - wall.Start.X) / wall.Length * opening.Offset,
            wall.Start.Y + (wall.End.Y - wall.Start.Y) / wall.Length * opening.Offset);
        check(Math.Abs(tag.Position.DistanceTo(onAxis) - (wall.Thickness / 2 + 0.35)) < 1e-6,
            "An opening tag sits beside its host wall at the opening's offset");

        check(ElementTags.Build(demo, demoLevel, new HashSet<TagCategory> { TagCategory.Room }).Count == demo.Rooms.Count(r => r.LevelId == demoLevel),
            "Room tags follow the requested categories");

        var session = new DocumentSession(demo);
        var added = session.AutoDimensionLevel(demoLevel);
        var expected = demo.Walls.Count(w => w.LevelId == demoLevel) + demo.Openings.Count;
        check(added == expected && session.Document.Dimensions.Count == expected, "Auto dimension covers every wall and opening");
        check(session.AutoDimensionLevel(demoLevel) == 0, "Auto dimension is idempotent");
        session.Undo();
        check(session.Document.Dimensions.Count == added, "Auto dimension is one undo step (the repeat added nothing)");
        session.Undo();
        check(session.Document.Dimensions.Count == 0, "Undoing auto dimension removes all of them");

        var context = new AgentContext(new DocumentSession(ModernHouseShowcase.Create()));
        var registry = ToolRegistry.CreateDefault();
        string Run(string name, string json) => registry.Find(name)!.Run(context,
            new ToolArguments(JsonNode.Parse(json)!.AsObject(), context.Session));
        var name = context.Session.Document.Levels.Single(level => level.Role == LevelRole.BaseFloor).Name;
        check(Run("get_tags", $$"""{"level":"{{name}}"}""").Contains("\"P1\"") &&
            Run("auto_dimension", $$"""{"level":"{{name}}"}""").StartsWith("Added") &&
            Run("get_area_schedule", "{}").Contains("Total"), "Assistant tools expose tags, auto dimensions and the area table");
    }
}
