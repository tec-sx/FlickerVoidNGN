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

    /// <summary>An override built in code: the values of a mood that the names spell, as files do.</summary>
    public static MoodOverride Of(LightingMood values, params IReadOnlyList<string> names)
    {
        List<MoodValue> named = [];
        foreach (string name in names)
        {
            MoodValue value = MoodValues.Find(name) ?? throw new ArgumentException($"No mood value is named '{name}'.", nameof(names));
            named.Add(value);
        }

        return new MoodOverride(values, named);
    }

    /// <summary>The whole mood the override makes over the defaults, for what draws with one mood file and nothing under it.</summary>
    public LightingMood ToMood()
    {
        LightingMood mood = new();
        Apply(mood);
        return mood;
    }

    /// <summary>Writes the named values onto a mood and leaves its others alone.</summary>
    public void Apply(LightingMood mood)
    {
        for (int index = 0; index < Named.Count; index++)
        {
            Named[index].Copy(Values, mood);
        }
    }
}
