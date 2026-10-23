namespace Guanacaste3D.Core;

/// <summary>Validated document editing and undo/redo history, independent of the UI.</summary>
public sealed class DocumentSession
{
    private readonly Stack<BuildingDocument> _undo = new();
    private readonly Stack<BuildingDocument> _redo = new();
    public BuildingDocument Document { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;

    public DocumentSession(BuildingDocument document)
    {
        document.Validate();
        Document = document.Clone();
    }

    private void Commit(Action<BuildingDocument> action)
    {
        var copy = Document.Clone();
        action(copy);
        copy.Validate();
        _undo.Push(Document);
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

    public void RemoveWall(Guid id)
    {
        if (!Document.Walls.Any(w => w.Id == id)) return;
        Commit(doc => doc.Walls.RemoveAll(w => w.Id == id));
    }

    public void Rename(string name) => Commit(doc => doc.Name = name.Trim());

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(Document);
        Document = _undo.Pop();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Push(Document);
        Document = _redo.Pop();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
