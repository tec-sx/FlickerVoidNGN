using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>Builds a 4/4 bar scale with phrases of four bars and a range of bars from 0 that is a number of pixels a bar wide.</summary>
internal sealed class BarScaleTestsBuilder
{
    private double _barPixels = 64;

    public BarScaleTestsBuilder WithBarPixels(double pixels)
    {
        _barPixels = pixels;
        return this;
    }

    public BarScale BuildScale() => new(beatsPerBar: 4, phraseBars: 4);

    public TimeRange BuildRange()
    {
        TimeRange range = new() { Earliest = 0 };
        range.Resize(_barPixels * 16);
        range.Show(0, 16);
        return range;
    }
}
