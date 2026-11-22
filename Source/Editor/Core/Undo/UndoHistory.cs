namespace FlickerVoid.Editor.Core.Undo;

/// <summary>
/// A document's undo and redo stacks: edits are recorded as steps, a new edit clears what could be redone, and the oldest steps go
/// past <see cref="MaxSteps"/> or <see cref="MaxBytes"/>. Nothing is recorded while a step is undone or redone.
/// </summary>
public sealed class UndoHistory
{
    private readonly LinkedList<UndoStep> _undo = [];
    private readonly Stack<UndoStep> _redo = new();
    private UndoGroup? _group;
    private int _groupDepth;
    private long _bytes;

    public int MaxSteps { get; init; } = 200;

    public long MaxBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>Whether a step is being undone or redone, when edits must not record themselves.</summary>
    public bool IsApplying { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoName => _undo.Last?.Value.Name;

    public string? RedoName
    {
        get
        {
            if (_redo.TryPeek(out UndoStep? step))
            {
                return step.Name;
            }

            return null;
        }
    }

    /// <summary>Raised when a step is recorded, undone or redone, or the history is cleared.</summary>
    public event Action? Changed;

    public void Record(UndoStep step)
    {
        if (IsApplying)
        {
            return;
        }

        if (_group is not null)
        {
            _group.Add(step);
            return;
        }

        Push(step);
    }

    /// <summary>Records every step until the matching <see cref="EndGroup"/> as one; groups inside it join the outer one.</summary>
    public void BeginGroup(string name)
    {
        if (IsApplying)
        {
            return;
        }

        _groupDepth++;
        _group ??= new UndoGroup(name);
    }

    public void EndGroup()
    {
        if (IsApplying || _groupDepth == 0)
        {
            return;
        }

        _groupDepth--;
        if (_groupDepth > 0)
        {
            return;
        }

        UndoGroup group = _group!;
        _group = null;
        if (group.Steps.Count == 1)
        {
            Push(group.Steps[0]);
        }
        else if (group.Steps.Count > 1)
        {
            Push(group);
        }
    }

    public bool Undo()
    {
        if (_group is not null || _undo.Last is null)
        {
            return false;
        }

        UndoStep step = _undo.Last.Value;
        _undo.RemoveLast();
        _bytes -= step.Size;
        Apply(step.Undo);
        _redo.Push(step);
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (_group is not null || !_redo.TryPop(out UndoStep? step))
        {
            return false;
        }

        Apply(step.Redo);
        _undo.AddLast(step);
        _bytes += step.Size;
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _group = null;
        _groupDepth = 0;
        _bytes = 0;
        Changed?.Invoke();
    }

    private void Push(UndoStep step)
    {
        _undo.AddLast(step);
        _bytes += step.Size;
        _redo.Clear();
        while (_undo.Count > 1 && (_undo.Count > MaxSteps || _bytes > MaxBytes))
        {
            UndoStep oldest = _undo.First!.Value;
            _undo.RemoveFirst();
            _bytes -= oldest.Size;
        }

        Changed?.Invoke();
    }

    private void Apply(Action action)
    {
        IsApplying = true;
        try
        {
            action();
        }
        finally
        {
            IsApplying = false;
        }
    }
}
