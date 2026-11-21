using FlickerVoid.Engine.Ai.Paths;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Ai;

/// <summary>Adds AI to a game: paths around obstacles, which characters then walk, when added after <c>AddCharacters</c>.</summary>
public static class AiGameExtensions
{
    public static PathSystem AddAi(this Game game, NavigationSettings? settings = null)
    {
        PathSystem paths = new(game.World, game.Lifecycle, settings ?? new NavigationSettings());
        game.Services.AddService(paths);
        CharacterMovementSystem? movement = game.Services.GetService<CharacterMovementSystem>();
        if (movement is not null)
        {
            movement.PathFinder = paths;
        }

        return paths;
    }
}
