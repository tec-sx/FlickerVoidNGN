using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>
/// The input actions a game reads and the keys, buttons and mouse movement that drive them, a data asset (<c>inputMap</c>), with the
/// contexts that limit which actions are read in a situation.
/// </summary>
public sealed class InputMap
{
    public List<InputActionDefinition> Actions { get; set; } = [];

    public List<InputContext> Contexts { get; set; } = [];

    /// <summary>The context with the id, or null when the map has none.</summary>
    public InputContext? FindContext(Tag id)
    {
        foreach (InputContext context in Contexts)
        {
            if (context.Id == id)
            {
                return context;
            }
        }

        return null;
    }
}
