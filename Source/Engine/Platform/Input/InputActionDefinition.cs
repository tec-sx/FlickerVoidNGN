using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>One action of an input map, like an Enhanced Input action with its key mappings: named by a tag and driven by its bindings.</summary>
public sealed class InputActionDefinition
{
    /// <summary>Names the action for the code that reads it, such as <c>Input.Move</c> or <c>Input.Interact</c>.</summary>
    public Tag Id { get; set; }

    public List<InputBinding> Bindings { get; set; } = [];
}
