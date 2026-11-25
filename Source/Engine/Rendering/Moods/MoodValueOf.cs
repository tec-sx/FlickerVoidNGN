using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>A mood value of a type, read and written without boxing.</summary>
public abstract class MoodValueOf<T>(string name, MoodGroup group, MoodOwner owner, Func<LightingMood, T> get, Action<LightingMood, T> set)
    : MoodValue(name, group, owner)
{
    public T Get(LightingMood mood) => get(mood);

    public void Set(LightingMood mood, T value) => set(mood, value);

    /// <summary>The value an amount of the way from one to another.</summary>
    public abstract T Blend(T from, T to, float amount);

    public sealed override void Copy(LightingMood from, LightingMood to)
    {
        T value = get(from);
        set(to, value);
    }

    public sealed override void Blend(LightingMood from, LightingMood to, float amount, LightingMood result)
    {
        T start = get(from);
        T end = get(to);
        T blended = Blend(start, end, amount);
        set(result, blended);
    }
}
