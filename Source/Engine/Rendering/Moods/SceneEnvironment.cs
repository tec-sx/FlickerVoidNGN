using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>Names the environment file a scene's light follows through the day, and sets the world clock when the scene plays.</summary>
public struct SceneEnvironment
{
    private static readonly QueryDescription Environments = new QueryDescription().WithAll<SceneEnvironment>();

    public SceneEnvironment()
    {
    }

    [ContentFile(".fvenv")]
    public string? Environment { get; set; }

    /// <summary>The game hours that pass in a real minute while the scene plays; 0 holds the clock at its hour.</summary>
    public float Speed { get; set; }

    /// <summary>The hour a new game starts at, when this is the first scene with an environment it plays.</summary>
    public float StartHour { get; set; } = 12;

    /// <summary>The environment file an entity of the world names, or null when none does; a scene names one.</summary>
    public static string? Find(World world)
    {
        string? found = null;
        world.Query(in Environments, (ref SceneEnvironment environment) =>
        {
            found ??= environment.Environment;
        });
        return found;
    }
}
