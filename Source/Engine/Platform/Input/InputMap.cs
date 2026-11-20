namespace FlickerVoid.Engine.Platform.Input;

/// <summary>The input actions a game reads and the keys, buttons and mouse movement that drive them, a data asset (<c>inputMap</c>).</summary>
public sealed class InputMap
{
    public List<InputActionDefinition> Actions { get; set; } = [];
}
