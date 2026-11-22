namespace FlickerVoid.Editor.Core.Undo;

/// <summary>Steps recorded as one change, such as a gizmo drag over several entities, undone in reverse order.</summary>
public sealed class UndoGroup : UndoStep
{
    private readonly List<UndoStep> _steps = [];

    public UndoGroup(string name)
        : base(name)
    {
    }

    public IReadOnlyList<UndoStep> Steps => _steps;

    public override long Size
    {
        get
        {
            long size = 0;
            foreach (UndoStep step in _steps)
            {
                size += step.Size;
            }

            return size;
        }
    }

    public void Add(UndoStep step)
    {
        foreach (UndoStep recorded in _steps)
        {
            if (recorded.TryMerge(step))
            {
                return;
            }
        }

        _steps.Add(step);
    }

    public override void Undo()
    {
        for (int i = _steps.Count - 1; i >= 0; i--)
        {
            _steps[i].Undo();
        }
    }

    public override void Redo()
    {
        foreach (UndoStep step in _steps)
        {
            step.Redo();
        }
    }
}
