using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>A named entity made to start from another entity template, or none; the components it gained are steps of their own.</summary>
internal sealed class TemplateStep(string name, EditorScene scene, string entity, string? before, string? after) : UndoStep(name)
{
    public override void Undo() => scene.ApplyTemplate(entity, before);

    public override void Redo() => scene.ApplyTemplate(entity, after);
}
