namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>What every time scale shares: the spacing of lines and labels, snapping and wrapping.</summary>
public static class TimeScales
{
    /// <summary>The fewest pixels between two lines between labels.</summary>
    public const double MinorSpacing = 12;

    /// <summary>The fewest pixels between two lines of a grid.</summary>
    public const double LineSpacing = 6;

    /// <summary>The first step whose length on screen is at least the spacing, or the last.</summary>
    public static double PickStep(ReadOnlySpan<double> steps, double pixelsPerUnit, double spacing)
    {
        foreach (double step in steps)
        {
            if (step * pixelsPerUnit >= spacing)
            {
                return step;
            }
        }

        return steps[^1];
    }

    /// <summary>The round step (1, 2 or 5 times a power of ten) whose length on screen is at least the spacing.</summary>
    public static double PickRoundStep(double pixelsPerUnit, double spacing)
    {
        double least = spacing / pixelsPerUnit;
        double power = Math.Pow(10, Math.Floor(Math.Log10(least)));
        double[] steps = [power, power * 2, power * 5, power * 10];
        return PickStep(steps, pixelsPerUnit, spacing);
    }

    public static double Snap(this ITimeScale scale, double time, TimeRange range)
    {
        double step = scale.GetSnapStep(range);
        return Math.Round(time / step) * step;
    }

    /// <summary>The time brought into one period when the scale repeats, else as it is.</summary>
    public static double Wrap(this ITimeScale scale, double time)
    {
        if (scale.Period is not { } period)
        {
            return time;
        }

        return ((time % period) + period) % period;
    }
}
