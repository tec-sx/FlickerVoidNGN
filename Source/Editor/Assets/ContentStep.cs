using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Assets;

/// <summary>An edit of a content file held unsaved: the whole file before it (null when it wasn't on disk yet) and after it.</summary>
internal sealed class ContentStep(UnsavedContent content, string path, byte[]? before, byte[] after) : UndoStep($"Edit {System.IO.Path.GetFileNameWithoutExtension(path)}")
{
    private byte[] _after = after;

    public override long Size => (before?.Length ?? 0) + _after.Length;

    public override void Undo() => content.Restore(path, before);

    public override void Redo() => content.Restore(path, _after);

    public override bool TryMerge(UndoStep next)
    {
        if (next is not ContentStep step)
        {
            return false;
        }

        _after = step._after;
        return true;
    }
}
