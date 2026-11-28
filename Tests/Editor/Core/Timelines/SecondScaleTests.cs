using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

public sealed class SecondScaleTests
{
    private const double Precision = 1e-6;

    private readonly SecondScaleTestsBuilder _builder = new();

    [Fact]
    public void LabelsRoundSecondsAtAReadableSpacing()
    {
        SecondScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Where(tick => tick.Label is not null).Select(tick => tick.Label).Should().StartWith(["0 s", "1 s", "2 s"]);
        ticks.Should().Contain(tick => Math.Abs(tick.Time - 0.2) < Precision && tick.Level == TickLevel.Minor);
    }

    [Fact]
    public void LabelsFractionsWhenZoomedIn()
    {
        SecondScale scale = _builder.BuildScale();
        TimeRange range = _builder.Showing(1).BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Should().Contain(tick => tick.Label == "0.1 s");
    }

    [Fact]
    public void LabelsTensWhenZoomedOut()
    {
        SecondScale scale = _builder.BuildScale();
        TimeRange range = _builder.Showing(100).BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Where(tick => tick.Label is not null).Select(tick => tick.Label).Should().StartWith(["0 s", "10 s"]);
    }

    [Fact]
    public void SnapsToTheFinestLineDrawn()
    {
        SecondScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();

        scale.Snap(1.33, range).Should().BeApproximately(1.4, Precision);
    }

    [Fact]
    public void FormatsSeconds()
    {
        SecondScale scale = _builder.BuildScale();

        scale.Format(1.25).Should().Be("1.25 s");
        scale.Period.Should().BeNull();
    }
}
