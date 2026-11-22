namespace FlickerVoid.Editor.Core.Undo;

/// <summary>One edit a document can take back and do again, holding the state before and after it rather than an inverse operation.</summary>
public abstract class UndoStep
{
    protected UndoStep(string name)
    {
        Name = name;
    }

    /// <summary>The name the Edit menu shows, such as "Move Lamp".</summary>
    public string Name { get; }

    /// <summary>About how many bytes the step holds, for the history's memory limit.</summary>
    public virtual long Size => 0;

    public abstract void Undo();

    public abstract void Redo();

    /// <summary>Takes in the next step of the same change when both touch the same thing, keeping this step's before and the next one's after.</summary>
    public virtual bool TryMerge(UndoStep next) => false;
}
