using Arch.Core;

using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>A named entity filed under another outline folder.</summary>
internal sealed class FolderStep(string name, EditorScene scene, string entity, string? before, string? after) : UndoStep(name)
{
    private readonly string _entity = entity;
    private string? _after = after;

    public override void Undo() => Apply(before);

    public override void Redo() => Apply(_after);

    public override bool TryMerge(UndoStep next)
    {
        if (next is not FolderStep step || step._entity != _entity)
        {
            return false;
        }

        _after = step._after;
        return true;
    }

    private void Apply(string? folder)
    {
        Entity? found = scene.Find(_entity);
        if (found is not null)
        {
            scene.SetFolder(found.Value, folder);
        }
    }
}
