using Arch.Core;

using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>A named entity nested under another parent, or taken off its parent (null).</summary>
internal sealed class ParentStep(string name, EditorScene scene, string entity, string? before, string? after) : UndoStep(name)
{
    public override void Undo() => Apply(before);

    public override void Redo() => Apply(after);

    private void Apply(string? parent)
    {
        Entity? found = scene.Find(entity);
        if (found is null)
        {
            return;
        }

        Entity? parentEntity = parent is null ? null : scene.Find(parent);
        scene.SetParent(found.Value, parentEntity);
    }
}
