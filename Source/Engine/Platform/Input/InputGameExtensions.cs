using FlickerVoid.Engine.Platform.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>Adds input to a game: input maps as data assets (<c>inputMap</c>), and with a window's input, the <see cref="InputActions"/> system.</summary>
public static class InputGameExtensions
{
    /// <summary>Registers the input map type, so the editor can create and edit maps; a game also reads them with <see cref="AddInputActions"/>.</summary>
    public static void AddInput(this Game game)
    {
        game.Data.AddResolver(PlatformJsonContext.Default);
        game.Data.Register<InputMap>("inputMap");
    }

    /// <summary>Reads input actions from <paramref name="input"/> each frame, before the game's frame systems, with an empty map until one is set.</summary>
    public static InputActions AddInputActions(this Game game, IInputState input)
    {
        InputActions actions = new(input, game.Events, new InputMap());
        game.FrameSystems.Add(actions);
        game.Services.AddService(actions);
        return actions;
    }
}
