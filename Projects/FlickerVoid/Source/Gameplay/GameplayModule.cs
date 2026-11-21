using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Gameplay.Serialization;
using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay;

/// <summary>FlickerVoid's gameplay: physics, point and click characters, interaction, the protagonist and doors; the click control only where there is a window to click in.</summary>
public sealed class GameplayModule : IGameModule
{
    public void Add(Game game)
    {
        IPhysicsSystem physics = game.AddPhysics();
        game.AddCharacters();
        game.AddInteraction();
        game.Spawner.Register("protagonist", FlickerVoidJsonContext.Default.Protagonist);
        game.Spawner.Register("doorSwing", FlickerVoidJsonContext.Default.DoorSwing);
        DoorSwingSystem doors = new(game);
        game.FixedSystems.Add(doors);

        IGameWindow? window = game.Services.GetService<IGameWindow>();
        IInputState? input = game.Services.GetService<IInputState>();
        if (window is not null && input is not null)
        {
            _ = new ClickControlSystem(game.World, game.Events, input, window, physics);
        }
    }
}
