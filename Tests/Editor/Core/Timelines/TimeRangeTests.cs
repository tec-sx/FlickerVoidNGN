using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

public sealed class TimeRangeTests
{
    private const double Precision = 1e-6;

    private readonly TimeRangeTestsBuilder _builder = new();

    [Fact]
    public void MapsTimeAcrossThePixels()
    {
        TimeRange range = _builder.Build();

        range.ToX(6).Should().BeApproximately(60, Precision);
        range.ToTime(180).Should().BeApproximately(18, Precision);
        range.PixelsPerUnit.Should().BeApproximately(10, Precision);
    }

    [Fact]
    public void PansByPixelsWithTheDrag()
    {
        TimeRange range = _builder.Build();

        range.Pan(20);

        range.MinTime.Should().BeApproximately(-2, Precision);
        range.MaxTime.Should().BeApproximately(22, Precision);
    }

    [Fact]
    public void NeverPansBeforeItsEarliestTime()
    {
        TimeRange range = _builder.StartingAt(0).Build();

        range.Pan(50);

        range.MinTime.Should().BeApproximately(0, Precision);
        range.MaxTime.Should().BeApproximately(24, Precision);
    }

    [Fact]
    public void ZoomsAboutTheTimeUnderThePointer()
    {
        TimeRange range = _builder.Build();

        range.ZoomAt(60, 3);

        range.ToTime(60).Should().BeApproximately(6, Precision);
        range.Span.Should().BeLessThan(24);
    }

    [Fact]
    public void ZoomsOutWithNegativeSteps()
    {
        TimeRange range = _builder.Build();

        range.ZoomAt(120, -2);

        range.ToTime(120).Should().BeApproximately(12, Precision);
        range.Span.Should().BeGreaterThan(24);
    }

    [Fact]
    public void StopsZoomingAtItsLimits()
    {
        TimeRange range = _builder.Build();

        range.ZoomAt(60, 500);
        double narrowest = range.Span;
        range.ZoomAt(60, -1000);

        narrowest.Should().BeApproximately(range.MinSpan, Precision);
        range.Span.Should().BeApproximately(range.MaxSpan, Precision);
    }

    [Fact]
    public void KeepsTheEarliestTimeWhenZoomingOut()
    {
        TimeRange range = _builder.StartingAt(0).Build();

        range.ZoomAt(240, -4);

        range.MinTime.Should().BeApproximately(0, Precision);
        range.Span.Should().BeGreaterThan(24);
    }

    [Fact]
    public void FramesASpanWithAShareLeftOnBothSides()
    {
        TimeRange range = _builder.Build();

        range.Frame(10, 20, 0.1);

        range.MinTime.Should().BeApproximately(9, Precision);
        range.MaxTime.Should().BeApproximately(21, Precision);
    }

    [Fact]
    public void FramesAPointAroundIt()
    {
        TimeRange range = _builder.Build();

        range.Frame(5, 5);

        range.MinTime.Should().BeLessThan(5);
        range.MaxTime.Should().BeGreaterThan(5);
    }

    [Fact]
    public void TellsOnceForEachChange()
    {
        TimeRange range = _builder.Build();
        int changes = 0;
        range.Changed += () => changes++;

        range.Pan(10);
        range.ZoomAt(0, 1);
        range.Resize(480);

        changes.Should().Be(3);
    }

    [Fact]
    public void StaysQuietWhenNothingChanges()
    {
        TimeRange range = _builder.Build();
        int changes = 0;
        range.Changed += () => changes++;

        range.Show(0, 24);
        range.Pan(0);
        range.Resize(240);

        changes.Should().Be(0);
    }

    [Fact]
    public void ReadsANarrowViewAsOnePixelWide()
    {
        TimeRange range = _builder.Build();

        range.Resize(0);

        range.Width.Should().Be(1);
    }
}
