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

    public DocumentSession(BuildingDocument document)
    {
        document.Validate();
        Document = document.Clone();
    }

    private static void PushSnapshot(List<BuildingDocument> list, BuildingDocument snapshot)
    {
        if (list.Count >= MaxHistory) list.RemoveAt(0);
        list.Add(snapshot);
    }

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

    public Guid AddWall(Guid levelId, Point2 start, Point2 end, double thickness = 0.20, double height = 3)
    {
        var id = Guid.NewGuid();
        Commit(doc => doc.Walls.Add(new Wall(id, levelId, start, end, thickness, height)));
        return id;
    }

    /// <summary>Add a rectangular slab, sorting opposite corners and validating atomically.</summary>
    public Guid AddSlab(Guid levelId, Point2 first, Point2 second, double thickness = 0.2)
    {
        var id = Guid.NewGuid();
        Commit(doc => doc.Slabs.Add(Slab.FromCorners(id, levelId, first, second, thickness)));
        return id;
    }

    public void UpdateSlab(Guid id, double thickness)
    {
        if (!Document.Slabs.Any(s => s.Id == id))
            throw new InvalidDataException("La losa seleccionada no existe.");
        Commit(doc =>
        {
            var index = doc.Slabs.FindIndex(s => s.Id == id);
            doc.Slabs[index] = doc.Slabs[index] with { Thickness = thickness };
        });
    }

    /// <summary>Resize from the fixed lower-left corner; invalid dimensions roll back.</summary>
    public void ResizeSlab(Guid id, double width, double depth)
    {
        if (!Document.Slabs.Any(s => s.Id == id))
            throw new InvalidDataException("La losa seleccionada no existe.");
        Commit(doc =>
        {
            var index = doc.Slabs.FindIndex(s => s.Id == id);
            var slab = doc.Slabs[index];
            doc.Slabs[index] = slab with
            {
                Max = new Point2(slab.Min.X + width, slab.Min.Y + depth)
            };
        });
    }

    /// <summary>Move a slab without changing its dimensions.</summary>
    public void MoveSlab(Guid id, double dx, double dy)
    {
        if (!Document.Slabs.Any(s => s.Id == id))
            throw new InvalidDataException("La losa seleccionada no existe.");
        Commit(doc =>
        {
            var index = doc.Slabs.FindIndex(s => s.Id == id);
            var slab = doc.Slabs[index];
            doc.Slabs[index] = slab with
            {
                Min = new Point2(slab.Min.X + dx, slab.Min.Y + dy),
                Max = new Point2(slab.Max.X + dx, slab.Max.Y + dy)
            };
        });
    }

    public Guid DuplicateSlab(Guid id, double dx = 1, double dy = 1)
    {
        var original = Document.Slabs.FirstOrDefault(s => s.Id == id)
            ?? throw new InvalidDataException("La losa seleccionada no existe.");
        var newId = Guid.NewGuid();
        Commit(doc => doc.Slabs.Add(original with
        {
            Id = newId,
            Min = new Point2(original.Min.X + dx, original.Min.Y + dy),
            Max = new Point2(original.Max.X + dx, original.Max.Y + dy)
        }));
        return newId;
    }

    public void RemoveSlab(Guid id)
    {
        if (!Document.Slabs.Any(s => s.Id == id)) return;
        Commit(doc => doc.Slabs.RemoveAll(s => s.Id == id));
    }

    public Guid AddLevel()
    {
        var id = Guid.NewGuid();
        Commit(doc =>
        {
            var elevation = doc.Levels.Max(l => l.Elevation) + 3;
            doc.Levels.Add(new Level(id, $"Nivel {doc.Levels.Count + 1}", elevation));
        });
        return id;
    }

    /// <summary>
    /// Create a type and attach it to one wall in a single undoable transaction.
    /// </summary>
    public Guid CreateWallTypeForWall(Guid wallId, string name, double thickness)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("Introduce un nombre para el tipo de muro.");
        var id = Guid.NewGuid();
        Commit(doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == wallId);
            if (index < 0) throw new InvalidDataException("El muro seleccionado no existe.");
            doc.WallTypes.Add(new WallType(id, name.Trim(), thickness));
            doc.Walls[index] = doc.Walls[index] with { TypeId = id, Thickness = thickness };
        });
        return id;
    }

    /// <summary>Assign a reusable wall type to an existing instance.</summary>
    public void SetWallType(Guid wallId, Guid typeId)
    {
        Commit(doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == wallId);
            var type = doc.WallTypes.FirstOrDefault(t => t.Id == typeId)
                ?? throw new InvalidDataException("El tipo de muro no existe.");
            if (index < 0) throw new InvalidDataException("El muro seleccionado no existe.");
            doc.Walls[index] = doc.Walls[index] with
            {
                TypeId = typeId,
                Thickness = type.Thickness
            };
        });
    }

    /// <summary>Detach a wall from its type, preserving its current thickness.</summary>
    public void DetachWallType(Guid wallId)
    {
        Commit(doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == wallId);
            if (index < 0) throw new InvalidDataException("El muro seleccionado no existe.");
            doc.Walls[index] = doc.Walls[index] with { TypeId = null };
        });
    }

    /// <summary>
    /// Update a type and propagate shared thickness to all its instances atomically.
    /// </summary>
    public void UpdateWallType(Guid typeId, string name, double thickness)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidDataException("Introduce un nombre para el tipo de muro.");
        Commit(doc =>
        {
            var index = doc.WallTypes.FindIndex(t => t.Id == typeId);
            if (index < 0) throw new InvalidDataException("El tipo de muro no existe.");
            doc.WallTypes[index] = doc.WallTypes[index] with
            {
                Name = name.Trim(),
                Thickness = thickness
            };
            for (var i = 0; i < doc.Walls.Count; i++)
                if (doc.Walls[i].TypeId == typeId)
                    doc.Walls[i] = doc.Walls[i] with { Thickness = thickness };
        });
    }

    /// <summary>Only unused wall types can be removed; attached walls are protected.</summary>
    public void RemoveWallType(Guid typeId)
    {
        Commit(doc =>
        {
            if (doc.Walls.Any(w => w.TypeId == typeId))
                throw new InvalidDataException("Desvincula los muros antes de eliminar el tipo.");
            if (doc.WallTypes.RemoveAll(t => t.Id == typeId) == 0)
                throw new InvalidDataException("El tipo de muro no existe.");
        });
    }

    public void UpdateWall(Guid id, double thickness, double height)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit(doc =>
        {
            var index = doc.Walls.FindIndex(w => w.Id == id);
            var wall = doc.Walls[index];
            if (wall.TypeId.HasValue && Math.Abs(thickness - wall.Thickness) > 1e-9)
                throw new InvalidDataException("El espesor depende del tipo: edita el tipo o desvincula el muro.");
            doc.Walls[index] = wall with { Thickness = thickness, Height = height };
        });
    }

    /// <summary>Adds a door or window opening to its existing host wall.</summary>
    public Guid AddOpening(Guid wallId, OpeningKind kind, double? offset = null,
        double width = 0.9, double sill = 0, double height = 2.1)
    {
        var wall = Document.Walls.FirstOrDefault(w => w.Id == wallId)
            ?? throw new InvalidDataException("El muro anfitrión no existe.");
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
            ?? throw new InvalidDataException("No existe el muro que deseas duplicar.");
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
            throw new InvalidDataException("La abertura no existe.");
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
            throw new InvalidDataException("Ángulo de rotación inválido.");
        if (!Document.Walls.Any(w => w.Id == id))
            throw new InvalidDataException("El muro no existe.");
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

    public void RemoveOpening(Guid id)
    {
        if (!Document.Openings.Any(o => o.Id == id)) return;
        Commit(doc => doc.Openings.RemoveAll(o => o.Id == id));
    }

    public void RemoveWall(Guid id)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit(doc =>
        {
            doc.Openings.RemoveAll(o => o.WallId == id);
            doc.Walls.RemoveAll(w => w.Id == id);
        });
    }

    public void Rename(string name) => Commit(doc => doc.Name = name.Trim());

    public void Undo()
    {
        if (!CanUndo) return;
        PushSnapshot(_redo, Document);
        Document = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        PushSnapshot(_undo, Document);
        Document = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
