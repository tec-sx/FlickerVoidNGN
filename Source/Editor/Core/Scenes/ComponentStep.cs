using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>A component of a named entity set, added or removed: its JSON before and after, none where it wasn't there.</summary>
internal sealed class ComponentStep : UndoStep
{
    private readonly EditorScene _scene;
    private readonly string _entity;
    private readonly SceneComponent _component;
    private readonly JsonElement? _before;
    private JsonElement? _after;

    public ComponentStep(string name, EditorScene scene, string entity, SceneComponent component, JsonElement? before, JsonElement? after)
        : base(name)
    {
        _scene = scene;
        _entity = entity;
        _component = component;
        _before = before;
        _after = after;
    }

    public override long Size => Measure(_before) + Measure(_after);

    public override void Undo() => Apply(_before);

    public override void Redo() => Apply(_after);

    public override bool TryMerge(UndoStep next)
    {
        if (next is not ComponentStep step || step._entity != _entity || step._component != _component)
        {
            return false;
        }

        _after = step._after;
        return true;
    }

    private void Apply(JsonElement? data)
    {
        Entity? entity = _scene.Find(_entity);
        if (entity is null)
        {
            return;
        }

        if (data is null)
        {
            _scene.RemoveComponent(entity.Value, _component);
            return;
        }

        _scene.ReadComponent(entity.Value, _component, data.Value);
    }

    private static long Measure(JsonElement? data)
    {
        if (data is null)
        {
            return 0;
        }

        string text = data.Value.GetRawText();
        return text.Length * sizeof(char);
    }
}
