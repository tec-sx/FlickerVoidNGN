namespace FlickerVoid.Editor.Core.Curves;

/// <summary>Where an edited key's time lands: snapped to a step when asked, and kept inside a looping curve's period.</summary>
internal static class CurveTimes
{
    /// <summary>How near a looping curve's last key may come to the end of its period: a minute of a day.</summary>
    private const float EndGap = 1f / 60;

    public static float Place(float time, float period, float step, bool snap)
    {
        if (snap && step > 0)
        {
            time = MathF.Round(time / step) * step;
        }

        if (period > 0)
        {
            time = Math.Clamp(time, 0, period - EndGap);
        }

        return time;
    }
}
