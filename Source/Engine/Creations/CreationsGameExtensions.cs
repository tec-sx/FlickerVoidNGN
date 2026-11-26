using FlickerVoid.Engine.Creations.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Creations;

/// <summary>Adds creations to a game.</summary>
public static class CreationsGameExtensions
{
    /// <summary>
    /// The game's <see cref="CreationParts"/>, a service added the first time with the <see cref="JointSystem"/> as a frame system and a service too,
    /// which each kind of creation spawns its parts with.
    /// </summary>
    public static CreationParts AddCreations(this Game game)
    {
        if (game.Services.GetService<CreationParts>() is { } added)
        {
            return added;
        }

        CreationParts parts = new(game.World, game.Lifecycle);
        game.Services.AddService(parts);
        JointSystem joints = new(game.World, game.Lifecycle, game.Events);
        game.FrameSystems.Add(joints);
        game.Services.AddService(joints);
        game.Data.AddResolver(CreationsJsonContext.Default);
        PartToggleSaves saves = new(game.World, parts, joints);
        game.SaveStates.Add<SavedPartToggles>("partToggles", saves.Save, saves.Restore);
        return parts;
    }
}
