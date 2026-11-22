using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Core.Tests.Undo;

/// <summary>Builds a history over one number, edited through steps that set it.</summary>
internal sealed class UndoHistoryTestsBuilder
{
    public UndoHistory History { get; private set; } = new();

    public int Value { get; set; }

    public UndoHistoryTestsBuilder WithLimits(int maxSteps, long maxBytes)
    {
        History = new UndoHistory { MaxSteps = maxSteps, MaxBytes = maxBytes };
        return this;
    }

    /// <summary>Sets the number and records the edit, as an editor's edit methods do.</summary>
    public void Set(int value, string name = "Set", long size = 0)
    {
        SetStep step = new(this, name, Value, value, size);
        Value = value;
        History.Record(step);
    }

    private sealed class SetStep(UndoHistoryTestsBuilder owner, string name, int before, int after, long size) : UndoStep(name)
    {
        private int _after = after;

        public override long Size => size;

        public override void Undo() => owner.Set(before);

        public override void Redo() => owner.Set(_after);

        public override bool TryMerge(UndoStep next)
        {
            if (next is not SetStep set)
            {
                return false;
            }

            _after = set._after;
            return true;
        }
    }
}
