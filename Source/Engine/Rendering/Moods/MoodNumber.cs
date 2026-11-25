using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>A number of a mood, which a curve can draw over time.</summary>
public sealed class MoodNumber(string name, MoodGroup group, MoodOwner owner, Func<LightingMood, float> get, Action<LightingMood, float> set)
    : MoodValueOf<float>(name, group, owner, get, set)
{
    public override float Blend(float from, float to, float amount) => float.Lerp(from, to, amount);
}
