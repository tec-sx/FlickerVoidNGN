using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// One value of a <see cref="LightingMood"/>: its name as files spell it, where it is edited, and how to copy and blend
/// it between moods.
/// </summary>
public abstract class MoodValue(string name, MoodGroup group, MoodOwner owner)
{
    public string Name { get; } = name;

    public MoodGroup Group { get; } = group;

    public MoodOwner Owner { get; } = owner;

    /// <summary>Writes one mood's value onto another mood.</summary>
    public abstract void Copy(LightingMood from, LightingMood to);

    /// <summary>Writes onto a mood the value an amount of the way from one mood's to another's.</summary>
    public abstract void Blend(LightingMood from, LightingMood to, float amount, LightingMood result);
}
