namespace Guanacaste3D.Core;

/// <summary>Validated document editing and undo/redo history, independent of the UI.</summary>
public sealed class DocumentSession
{
    private const int MaxHistory = 40;
    private readonly List<BuildingDocument> _undo = [];
    private readonly List<BuildingDocument> _redo = [];
    public BuildingDocument Document { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;

    /// <summary>Starts an isolated edit session from a validated document snapshot.</summary>
    public DocumentSession(BuildingDocument document)
    {
        document.Validate();
        Document = document.Clone();
    }

    /// <summary>Adds a history snapshot while enforcing the bounded-memory policy.</summary>
    private static void PushSnapshot(List<BuildingDocument> list, BuildingDocument snapshot)
    {
        if (list.Count >= MaxHistory) list.RemoveAt(0);
        list.Add(snapshot);
    }

    /// <summary>Validates and atomically commits a mutation to the active document.</summary>
    private void Commit(Action<BuildingDocument> action)
    {
        var copy = Document.Clone();
        action(copy);
        copy.Validate();
        PushSnapshot(_undo, Document);
        _redo.Clear();
        Document = copy;
        Changed?.Invoke(this, EventArgs.Empty);
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
        Commit(doc => doc.Walls.Add(new Wall(id, levelId, start, end, thickness, height)
        {
            WallTypeId = selectedTypeId
        }));
        return id;
    }

    /// <summary>Adds a level three meters above the current highest level.</summary>
    public Guid AddLevel()
    {
        var id = Guid.NewGuid();
        Commit(doc =>
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
        Commit(doc =>
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
        Commit(document =>
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
        Commit(document => document.WallTypes.Add(
            new WallType(id, name.Trim(), function, thickness, materialName.Trim())));
        return id;
    }

    /// <summary>Updates a wall type and propagates its thickness to every instance.</summary>
    public void UpdateWallType(Guid id, string name, WallFunction function, double thickness, string materialName)
    {
        if (!Document.WallTypes.Any(type => type.Id == id))
            throw new InvalidDataException("The wall type does not exist.");
        Commit(document =>
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
        Commit(document =>
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
        Commit(document => document.Floors.Add(new FloorSlab(
            id, levelId, name ?? $"Floor {document.Floors.Count + 1}", [.. boundary], thickness)));
        return id;
    }

    /// <summary>Adds a numbered drawing sheet to the project document set.</summary>
    public Guid AddSheet(SheetPaperSize paperSize = SheetPaperSize.A1, string? name = null)
    {
        var id = Guid.NewGuid();
        Commit(document =>
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
            Commit(document => document.Rooms.RemoveAll(room => room.Id == id));
    }

    /// <summary>Removes a floor slab from the project.</summary>
    public void RemoveFloor(Guid id)
    {
        if (Document.Floors.Any(floor => floor.Id == id))
            Commit(document => document.Floors.RemoveAll(floor => floor.Id == id));
    }

    /// <summary>Removes a drawing sheet from the project.</summary>
    public void RemoveSheet(Guid id)
    {
        if (Document.Sheets.Any(sheet => sheet.Id == id))
            Commit(document => document.Sheets.RemoveAll(sheet => sheet.Id == id));
    }

    /// <summary>Adds a door or window opening to its existing host wall.</summary>
    public Guid AddOpening(Guid wallId, OpeningKind kind, double? offset = null,
        double width = 0.9, double sill = 0, double height = 2.1)
    {
        var wall = Document.Walls.FirstOrDefault(w => w.Id == wallId)
            ?? throw new InvalidDataException("The host wall does not exist.");
        var id = Guid.NewGuid();
        Commit(doc => doc.Openings.Add(new WallOpening(id, wallId, kind,
            offset ?? wall.Length / 2, width, sill, height)));
        return id;
    }

    /// <summary>Moves a wall and its hosted openings without changing dimensions.</summary>
    public void MoveWall(Guid id, double deltaX, double deltaY)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit(doc =>
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
        Commit(doc =>
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

    /// <summary>Edits a hosted opening atomically; invalid geometry never enters the document.</summary>
    public void UpdateOpening(Guid id, double offset, double width, double sill, double height)
    {
        if (!Document.Openings.Any(o => o.Id == id))
            throw new InvalidDataException("The opening does not exist.");
        Commit(doc =>
        {
            var index = doc.Openings.FindIndex(o => o.Id == id);
            doc.Openings[index] = doc.Openings[index] with
            {
                Offset = offset, Width = width, Sill = sill, Height = height
            };
        });
    }

    /// <summary>Rotates a straight wall around its starting point, retaining hosted openings.</summary>
    public void RotateWall(Guid id, double degrees)
    {
        if (!double.IsFinite(degrees))
            throw new InvalidDataException("The rotation angle is invalid.");
        if (!Document.Walls.Any(w => w.Id == id))
            throw new InvalidDataException("The wall does not exist.");
        var radians = degrees * Math.PI / 180;
        Commit(doc =>
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
        Commit(doc => doc.Openings.RemoveAll(o => o.Id == id));
    }

    /// <summary>Removes a wall and cascades removal to its hosted openings.</summary>
    public void RemoveWall(Guid id)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit(doc =>
        {
            doc.Openings.RemoveAll(o => o.WallId == id);
            doc.Walls.RemoveAll(w => w.Id == id);
        });
    }

    /// <summary>Renames the active project after trimming surrounding whitespace.</summary>
    public void Rename(string name) => Commit(document => document.Name = name.Trim());

    /// <summary>Restores the previous validated document snapshot when available.</summary>
    public void Undo()
    {
        if (!CanUndo) return;
        PushSnapshot(_redo, Document);
        Document = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reapplies the next validated document snapshot when available.</summary>
    public void Redo()
    {
        if (!CanRedo) return;
        PushSnapshot(_undo, Document);
        Document = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
