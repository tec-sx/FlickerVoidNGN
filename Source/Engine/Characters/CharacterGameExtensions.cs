using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Characters.Serialization;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Characters;

/// <summary>Adds point and click characters to a game: the <c>characterMover</c> component and the movement system (a service, whose path finder a paths module sets), after physics when the game has it.</summary>
public static class CharacterGameExtensions
{
    public static CharacterMovementSystem AddCharacters(this Game game)
    {
        game.Spawner.Register("characterMover", CharactersJsonContext.Default.CharacterMover);
        IPhysicsSystem? physics = game.Services.GetService<IPhysicsSystem>();
        CharacterMovementSystem movement = new(game.World, game.Events, physics);
        game.FixedSystems.Add(movement);
        game.Services.AddService(movement);
        return movement;
    }
}
