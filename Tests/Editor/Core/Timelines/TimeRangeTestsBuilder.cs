using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>Builds a range showing 0 to 24 on a view 240 pixels wide: 10 pixels a unit.</summary>
internal sealed class TimeRangeTestsBuilder
{
    private double? _earliest;

    public TimeRangeTestsBuilder StartingAt(double earliest)
    {
        _earliest = earliest;
        return this;
    }

    public TimeRange Build()
    {
        TimeRange range = new() { Earliest = _earliest };
        range.Resize(240);
        range.Show(0, 24);
        return range;
    }
}
