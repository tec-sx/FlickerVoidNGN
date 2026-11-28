using System.Globalization;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>Time in hours of a day that wraps at 24, labelled as clock times, snapping to quarter hours.</summary>
public sealed class HourScale : ITimeScale
{
    public const double Day = 24;

    /// <summary>The fewest pixels between two clock labels.</summary>
    public const double LabelSpacing = 90;

    public const double SnapStep = 0.25;

    private static readonly double[] LabelSteps = [1, 2, 3, 6, 12, 24];

    public string Unit => "hours";

    public double? Period => Day;

    public void GetTicks(TimeRange range, List<TimeTick> ticks)
    {
        ticks.Clear();
        double perHour = range.PixelsPerUnit;
        int labelStep = (int)TimeScales.PickStep(LabelSteps, perHour, LabelSpacing);
        int hourStep = labelStep;
        if (perHour >= TimeScales.LineSpacing)
        {
            hourStep = 1;
        }

        int first = (int)Math.Floor(range.MinTime / hourStep) * hourStep;
        for (int time = first; time <= range.MaxTime; time += hourStep)
        {
            int hour = (int)this.Wrap(time);
            TickLevel level = TickLevel.Minor;
            string? label = null;
            if (hour % labelStep == 0)
            {
                level = TickLevel.Major;
                label = string.Create(CultureInfo.InvariantCulture, $"{hour:00}:00");
            }

            if (hour == 0)
            {
                level = TickLevel.Section;
            }

            ticks.Add(new TimeTick(time, level, label));
        }
    }

    public double GetSnapStep(TimeRange range) => SnapStep;

    /// <summary>The clock time within the day ("18:30").</summary>
    public string Format(double time)
    {
        int minutes = (int)Math.Round(this.Wrap(time) * 60);
        int hour = minutes / 60 % (int)Day;
        int minute = minutes % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hour:00}:{minute:00}");
    }
}
