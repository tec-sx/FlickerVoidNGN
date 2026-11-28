namespace FlickerVoid.Editor.Core.Music;

/// <summary>
/// How the mixer's meters move: up to the loudest sample at once, and back down evenly on their decibel scale at
/// <see cref="FallDecibels"/> a second, so a meter falls smoothly from a hit to the next instead of dropping out.
/// </summary>
public static class MeterBallistics
{
    /// <summary>How fast a meter falls: 0 dB to the meter's floor of -48 dB in a second.</summary>
    public const double FallDecibels = 48;

    /// <summary>The level, -60 dB, under which a falling meter rests at 0.</summary>
    public const double Floor = 0.001;

    /// <summary>The level to show after <paramref name="elapsed"/>, from the one shown and the loudest sample since.</summary>
    public static double Follow(double shown, double peak, TimeSpan elapsed)
    {
        double exponent = -FallDecibels * elapsed.TotalSeconds / 20;
        double fallen = shown * Math.Pow(10, exponent);
        if (fallen < Floor)
        {
            fallen = 0;
        }

        return Math.Max(peak, fallen);
    }
}
