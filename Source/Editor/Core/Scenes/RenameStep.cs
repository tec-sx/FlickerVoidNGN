using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>An entity renamed, with what was nested under it following the name; the references that named it are steps of their own.</summary>
internal sealed class RenameStep(string name, EditorScene scene, string before, string after) : UndoStep(name)
{
    public override void Undo() => scene.ApplyRename(after, before);

    public override void Redo() => scene.ApplyRename(before, after);
}
