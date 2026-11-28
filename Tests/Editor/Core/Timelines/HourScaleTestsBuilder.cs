using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>Builds the hour scale and a range showing a day on a number of pixels an hour.</summary>
internal sealed class HourScaleTestsBuilder
{
    private double _hourPixels = 40;

    public HourScaleTestsBuilder WithHourPixels(double pixels)
    {
        _hourPixels = pixels;
        return this;
    }

    public HourScale BuildScale() => new();

    public TimeRange BuildRange()
    {
        TimeRange range = new();
        range.Resize(_hourPixels * 24);
        range.Show(0, 24);
        return range;
    }
}
