using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>A color of a mood, which a color curve can draw over time.</summary>
public sealed class MoodColor(string name, MoodGroup group, MoodOwner owner, Func<LightingMood, Vector3> get, Action<LightingMood, Vector3> set)
    : MoodValueOf<Vector3>(name, group, owner, get, set)
{
    public override Vector3 Blend(Vector3 from, Vector3 to, float amount) => Vector3.Lerp(from, to, amount);
}
