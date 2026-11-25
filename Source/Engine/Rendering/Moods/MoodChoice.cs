using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>A mood value that is one thing or another, such as a canvas, and so swaps halfway through a blend.</summary>
public sealed class MoodChoice<T>(string name, MoodGroup group, MoodOwner owner, Func<LightingMood, T> get, Action<LightingMood, T> set)
    : MoodValueOf<T>(name, group, owner, get, set)
{
    public override T Blend(T from, T to, float amount)
    {
        if (amount >= 0.5f)
        {
            return to;
        }

        return from;
    }
}
