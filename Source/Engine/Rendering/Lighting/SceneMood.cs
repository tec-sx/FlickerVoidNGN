using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>Names the lighting mood file a scene is drawn with, unless the game picks another.</summary>
public struct SceneMood
{
    private static readonly QueryDescription Moods = new QueryDescription().WithAll<SceneMood>();

    [ContentFile(".fvmood")]
    public string? Mood { get; set; }

    /// <summary>The mood file an entity of the world names, or null when none does; a scene names one.</summary>
    public static string? Find(World world)
    {
        string? found = null;
        world.Query(in Moods, (ref SceneMood mood) =>
        {
            found ??= mood.Mood;
        });
        return found;
    }
}
