using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>Builds the second scale and a range of a number of seconds on 600 pixels.</summary>
internal sealed class SecondScaleTestsBuilder
{
    private double _seconds = 10;

    public SecondScaleTestsBuilder Showing(double seconds)
    {
        _seconds = seconds;
        return this;
    }

    public SecondScale BuildScale() => new();

    public TimeRange BuildRange()
    {
        TimeRange range = new();
        range.Resize(600);
        range.Show(0, _seconds);
        return range;
    }
}
