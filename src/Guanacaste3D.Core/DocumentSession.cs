namespace Guanacaste3D.Core;

/// <summary>Validated document editing and undo/redo history, independent of the UI.</summary>
public sealed partial class DocumentSession
{
    public BuildingDocument Document { get; private set; }

    /// <summary>Starts an isolated edit session from a validated document snapshot.</summary>
    public DocumentSession(BuildingDocument document)
    {
        document.Validate();
        Document = document.Clone();
    }

    /// <summary>Adds a straight wall instance using a valid project wall type.</summary>
    public Guid AddWall(Guid levelId, Point2 start, Point2 end, double thickness = 0.20,
        double height = 3, Guid? wallTypeId = null)
    {
        var selectedTypeId = wallTypeId ?? BuiltInWallTypes.Basic200Id;
        if (!Document.WallTypes.Any(type => type.Id == selectedTypeId))
            throw new InvalidDataException("The selected wall type does not exist.");
        if (wallTypeId is not null)
            thickness = Document.WallTypes.Single(type => type.Id == selectedTypeId).Thickness;
        var id = Guid.NewGuid();
        Commit("Transaction.AddWall", doc => doc.Walls.Add(new Wall(id, levelId, start, end, thickness, height)
        {
            WallTypeId = selectedTypeId
        }));
        return id;
    }

    /// <summary>Adds a level three meters above the current highest level.</summary>
    public Guid AddLevel()
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddLevel", doc =>
        {
            var elevation = doc.Levels.Max(l => l.Elevation) + 3;
            var level = new Level(id, $"Level {doc.Levels.Count + 1}", elevation);
            doc.Levels.Add(level);
            doc.Views.Add(BuiltInProjectViews.CreatePlan(level));
        });
        return id;
    }

    /// <summary>Updates instance dimensions for an existing wall.</summary>
    public void UpdateWall(Guid id, double thickness, double height)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit("Transaction.UpdateWall", doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == id);
            doc.Walls[index] = doc.Walls[index] with { Thickness = thickness, Height = height };
        });
    }

    /// <summary>Assigns a reusable type and its thickness to an existing wall.</summary>
    public void SetWallType(Guid wallId, Guid wallTypeId)
    {
        var type = Document.WallTypes.FirstOrDefault(candidate => candidate.Id == wallTypeId)
            ?? throw new InvalidDataException("The selected wall type does not exist.");
        if (!Document.Walls.Any(wall => wall.Id == wallId))
            throw new InvalidDataException("The selected wall does not exist.");
        Commit("Transaction.SetWallType", document =>
        {
            var index = document.Walls.FindIndex(wall => wall.Id == wallId);
            document.Walls[index] = document.Walls[index] with
            {
                WallTypeId = wallTypeId,
                Thickness = type.Thickness
            };
        });
    }

    /// <summary>Adds a reusable wall type to the project catalog.</summary>
    public Guid AddWallType(string name, WallFunction function, double thickness, string materialName)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddWallType", document => document.WallTypes.Add(
            new WallType(id, name.Trim(), function, thickness, materialName.Trim())));
        return id;
    }

    /// <summary>Updates a wall type and propagates its thickness to every instance.</summary>
    public void UpdateWallType(Guid id, string name, WallFunction function, double thickness, string materialName)
    {
        if (!Document.WallTypes.Any(type => type.Id == id))
            throw new InvalidDataException("The wall type does not exist.");
        Commit("Transaction.UpdateWallType", document =>
        {
            var typeIndex = document.WallTypes.FindIndex(type => type.Id == id);
            document.WallTypes[typeIndex] = new WallType(
                id, name.Trim(), function, thickness, materialName.Trim());
            for (var wallIndex = 0; wallIndex < document.Walls.Count; wallIndex++)
            {
                if (document.Walls[wallIndex].WallTypeId == id)
                    document.Walls[wallIndex] = document.Walls[wallIndex] with { Thickness = thickness };
            }
        });
    }

    /// <summary>Adds a rectangular room annotation to a level.</summary>
    public Guid AddRoom(Guid levelId, Point2 firstCorner, Point2 secondCorner, string? name = null)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddRoom", document =>
        {
            var number = (document.Rooms.Count + 1).ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            document.Rooms.Add(new Room(id, levelId, number, name ?? $"Room {number}", firstCorner, secondCorner));
        });
        return id;
    }

    /// <summary>Adds a floor slab using a caller-supplied closed plan boundary.</summary>
    public Guid AddFloor(Guid levelId, IReadOnlyCollection<Point2> boundary, double thickness = 0.20,
        string? name = null)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddFloor", document => document.Floors.Add(new FloorSlab(
            id, levelId, name ?? $"Floor {document.Floors.Count + 1}", [.. boundary], thickness)));
        return id;
    }

    /// <summary>Adds a numbered drawing sheet to the project document set.</summary>
    public Guid AddSheet(SheetPaperSize paperSize = SheetPaperSize.A1, string? name = null)
    {
        var id = Guid.NewGuid();
        Commit("Transaction.AddSheet", document =>
        {
            var index = document.Sheets.Count + 1;
            document.Sheets.Add(new DrawingSheet(id, $"A{index:D3}", name ?? $"Sheet {index}", paperSize));
        });
        return id;
    }

    /// <summary>Removes a room annotation from the project.</summary>
    public void RemoveRoom(Guid id)
    {
        if (Document.Rooms.Any(room => room.Id == id))
            Commit("Transaction.RemoveRoom", document => document.Rooms.RemoveAll(room => room.Id == id));
    }

    /// <summary>Removes a floor slab from the project.</summary>
    public void RemoveFloor(Guid id)
    {
        if (Document.Floors.Any(floor => floor.Id == id))
            Commit("Transaction.RemoveFloor", document => document.Floors.RemoveAll(floor => floor.Id == id));
    }

    /// <summary>Removes a drawing sheet from the project.</summary>
    public void RemoveSheet(Guid id)
    {
        if (Document.Sheets.Any(sheet => sheet.Id == id))
            Commit("Transaction.RemoveSheet", document => document.Sheets.RemoveAll(sheet => sheet.Id == id));
    }

    /// <summary>Moves a wall and its hosted openings without changing dimensions.</summary>
    public void MoveWall(Guid id, double deltaX, double deltaY)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit("Transaction.MoveWall", doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == id);
            var wall = doc.Walls[index];
            doc.Walls[index] = wall with
            {
                Start = new Point2(wall.Start.X + deltaX, wall.Start.Y + deltaY),
                End = new Point2(wall.End.X + deltaX, wall.End.Y + deltaY)
            };
        });
    }

    /// <summary>Duplicates a wall and all of its openings with new stable identifiers.</summary>
    public Guid DuplicateWall(Guid id, double deltaX = 1, double deltaY = 1)
    {
        var original = Document.Walls.FirstOrDefault(w => w.Id == id)
            ?? throw new InvalidDataException("The wall to duplicate does not exist.");
        var newId = Guid.NewGuid();
        Commit("Transaction.DuplicateWall", doc =>
        {
            doc.Walls.Add(original with
            {
                Id = newId,
                Start = new Point2(original.Start.X + deltaX, original.Start.Y + deltaY),
                End = new Point2(original.End.X + deltaX, original.End.Y + deltaY)
            });
            foreach (var opening in doc.Openings.Where(o => o.WallId == id).ToArray())
                doc.Openings.Add(opening with { Id = Guid.NewGuid(), WallId = newId });
        });
        return newId;
    }

    /// <summary>Rotates a straight wall around its starting point, retaining hosted openings.</summary>
    public void RotateWall(Guid id, double degrees)
    {
        if (!double.IsFinite(degrees))
            throw new InvalidDataException("The rotation angle is invalid.");
        if (!Document.Walls.Any(w => w.Id == id))
            throw new InvalidDataException("The wall does not exist.");
        var radians = degrees * Math.PI / 180;
        Commit("Transaction.RotateWall", doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == id);
            var wall = doc.Walls[index];
            var dx = wall.End.X - wall.Start.X;
            var dy = wall.End.Y - wall.Start.Y;
            doc.Walls[index] = wall with
            {
                End = new Point2(
                    wall.Start.X + dx * Math.Cos(radians) - dy * Math.Sin(radians),
                    wall.Start.Y + dx * Math.Sin(radians) + dy * Math.Cos(radians))
            };
        });
    }

    /// <summary>Removes a hosted opening when it exists.</summary>
    public void RemoveOpening(Guid id)
    {
        if (!Document.Openings.Any(o => o.Id == id)) return;
        Commit("Transaction.RemoveOpening", doc => doc.Openings.RemoveAll(o => o.Id == id));
    }

    /// <summary>Removes a wall and cascades removal to its hosted openings.</summary>
    public void RemoveWall(Guid id)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit("Transaction.RemoveWall", doc =>
        {
            doc.Openings.RemoveAll(o => o.WallId == id);
            doc.Walls.RemoveAll(w => w.Id == id);
        });
    }

    /// <summary>Renames the active project after trimming surrounding whitespace.</summary>
    public void Rename(string name) => Commit("Transaction.Rename", document => document.Name = name.Trim());
}
