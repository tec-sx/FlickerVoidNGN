using System.Globalization;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>Time in bars from the first bar's start, labelled from 1, with beats between and phrases marked; it does not wrap.</summary>
public sealed class BarScale(int beatsPerBar, int phraseBars) : ITimeScale
{
    /// <summary>The fewest pixels between two bar labels.</summary>
    public const double LabelSpacing = 28;

    private const int SixteenthsPerBeat = 4;

    private static readonly double[] LabelSteps = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512];

    public int BeatsPerBar { get; } = Math.Max(beatsPerBar, 1);

    public int PhraseBars { get; } = Math.Max(phraseBars, 1);

    public string Unit => "bars";

    public double? Period => null;

    public void GetTicks(TimeRange range, List<TimeTick> ticks)
    {
        ticks.Clear();
        double perBar = range.PixelsPerUnit;
        int labelStep = (int)TimeScales.PickStep(LabelSteps, perBar, LabelSpacing);
        int barStep = labelStep;
        if (perBar >= TimeScales.LineSpacing)
        {
            barStep = 1;
        }

        bool beats = perBar / BeatsPerBar >= TimeScales.MinorSpacing;
        int first = Math.Max((int)Math.Floor(range.MinTime / barStep) * barStep, 0);
        for (int bar = first; bar <= range.MaxTime; bar += barStep)
        {
            string? label = null;
            if (bar % labelStep == 0)
            {
                label = (bar + 1).ToString(CultureInfo.InvariantCulture);
            }

            TickLevel level = TickLevel.Major;
            if (bar % PhraseBars == 0)
            {
                level = TickLevel.Section;
            }

            ticks.Add(new TimeTick(bar, level, label));
            if (!beats)
            {
                continue;
            }

            for (int beat = 1; beat < BeatsPerBar; beat++)
            {
                double time = bar + ((double)beat / BeatsPerBar);
                ticks.Add(new TimeTick(time, TickLevel.Minor, null));
            }
        }
    }

    /// <summary>A beat when beats are drawn, else a bar, else the labelled step.</summary>
    public double GetSnapStep(TimeRange range)
    {
        double perBar = range.PixelsPerUnit;
        if (perBar / BeatsPerBar >= TimeScales.MinorSpacing)
        {
            return 1.0 / BeatsPerBar;
        }

        if (perBar >= TimeScales.LineSpacing)
        {
            return 1;
        }

        return TimeScales.PickStep(LabelSteps, perBar, LabelSpacing);
    }

    /// <summary>Bar, beat and sixteenth, each from 1 ("12.2.1").</summary>
    public string Format(double time)
    {
        double clamped = Math.Max(time, 0);
        int sixteenths = (int)Math.Floor((clamped * BeatsPerBar * SixteenthsPerBeat) + 1e-6);
        int perBar = BeatsPerBar * SixteenthsPerBeat;
        int bar = (sixteenths / perBar) + 1;
        int beat = ((sixteenths % perBar) / SixteenthsPerBeat) + 1;
        int sixteenth = (sixteenths % SixteenthsPerBeat) + 1;
        return string.Create(CultureInfo.InvariantCulture, $"{bar}.{beat}.{sixteenth}");
    }
}
