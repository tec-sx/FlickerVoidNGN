using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// A mood file read as what it changes: only the values it names, laid over whatever mood is under it, where a
/// <see cref="LightingMood"/> read from the same file is whole, with defaults for the rest.
/// </summary>
[JsonConverter(typeof(MoodOverrideJsonConverter))]
public sealed class MoodOverride(LightingMood values, IReadOnlyList<MoodValue> named)
{
    /// <summary>A mood holding the named values; its others mean nothing.</summary>
    public LightingMood Values { get; } = values;

    /// <summary>The values the override changes, in its file's order.</summary>
    public IReadOnlyList<MoodValue> Named { get; } = named;

    /// <summary>Writes the named values onto a mood and leaves its others alone.</summary>
    public void Apply(LightingMood mood)
    {
        for (int index = 0; index < Named.Count; index++)
        {
            Named[index].Copy(Values, mood);
        }
    }
}
