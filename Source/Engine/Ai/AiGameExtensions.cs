using FlickerVoid.Engine.Ai.Paths;
using FlickerVoid.Engine.Ai.Senses;
using FlickerVoid.Engine.Ai.Serialization;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Ai;

/// <summary>
/// Adds AI to a game: paths around obstacles, which characters then walk when added after <c>AddCharacters</c>, and the
/// <c>perception</c> and <c>perceivable</c> components with the system that lets perceivers see and hear (both services).
/// </summary>
public static class AiGameExtensions
{
    public static PathSystem AddAi(this Game game, NavigationSettings? settings = null)
    {
        game.Spawner.Register("perception", AiJsonContext.Default.Perception);
        game.Spawner.Register("perceivable", AiJsonContext.Default.Perceivable);
        PathSystem paths = new(game.World, game.Lifecycle, settings ?? new NavigationSettings());
        game.Services.AddService(paths);
        CharacterMovementSystem? movement = game.Services.GetService<CharacterMovementSystem>();
        if (movement is not null)
        {
            movement.PathFinder = paths;
        }

        IPhysicsSystem? physics = game.Services.GetService<IPhysicsSystem>();
        PerceptionSystem perception = new(game.World, game.Events, game.Lifecycle, physics);
        game.FixedSystems.Add(perception);
        game.Services.AddService(perception);
        return paths;
    }
}
