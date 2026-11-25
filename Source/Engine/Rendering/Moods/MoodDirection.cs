using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>A direction of a mood, which turns between two moods rather than shortening.</summary>
public sealed class MoodDirection(string name, MoodGroup group, MoodOwner owner, Func<LightingMood, Vector3> get, Action<LightingMood, Vector3> set)
    : MoodValueOf<Vector3>(name, group, owner, get, set)
{
    private const float MinLengthSquared = 1e-12f;

    /// <summary>The direction between the two, or the one blended toward when they are opposite and meet at nothing.</summary>
    public override Vector3 Blend(Vector3 from, Vector3 to, float amount)
    {
        Vector3 between = Vector3.Lerp(from, to, amount);
        if (between.LengthSquared() <= MinLengthSquared)
        {
            return to;
        }

        return Vector3.Normalize(between);
    }
}
