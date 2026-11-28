using System.Globalization;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>Time in seconds, labelled at round steps with lines between; it does not wrap.</summary>
public sealed class SecondScale : ITimeScale
{
    /// <summary>The fewest pixels between two labels.</summary>
    public const double LabelSpacing = 56;

    public string Unit => "seconds";

    public double? Period => null;

    public void GetTicks(TimeRange range, List<TimeTick> ticks)
    {
        ticks.Clear();
        double perSecond = range.PixelsPerUnit;
        double labelStep = TimeScales.PickRoundStep(perSecond, LabelSpacing);
        int parts = GetParts(labelStep, perSecond);
        double step = labelStep / parts;
        long first = (long)Math.Floor(range.MinTime / step);
        long last = (long)Math.Ceiling(range.MaxTime / step);
        for (long index = first; index <= last; index++)
        {
            double time = index * step;
            if (time < range.MinTime - 1e-9 || time > range.MaxTime + 1e-9)
            {
                continue;
            }

            if (index % parts != 0)
            {
                ticks.Add(new TimeTick(time, TickLevel.Minor, null));
                continue;
            }

            ticks.Add(new TimeTick(time, TickLevel.Major, Format(time)));
        }
    }

    /// <summary>The finest line drawn.</summary>
    public double GetSnapStep(TimeRange range)
    {
        double perSecond = range.PixelsPerUnit;
        double labelStep = TimeScales.PickRoundStep(perSecond, LabelSpacing);
        int parts = GetParts(labelStep, perSecond);
        return labelStep / parts;
    }

    public string Format(double time) => string.Create(CultureInfo.InvariantCulture, $"{time:0.##} s");

    /// <summary>How many parts a labelled step is split into by lines: five, two, or none when they would crowd.</summary>
    private static int GetParts(double labelStep, double perSecond)
    {
        if (labelStep / 5 * perSecond >= TimeScales.MinorSpacing)
        {
            return 5;
        }

        if (labelStep / 2 * perSecond >= TimeScales.MinorSpacing)
        {
            return 2;
        }

        return 1;
    }
}
