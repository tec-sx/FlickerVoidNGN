using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

public sealed class BarScaleTests
{
    private const double Precision = 1e-6;

    private readonly BarScaleTestsBuilder _builder = new();

    [Fact]
    public void LabelsEveryBarCountingFromOne()
    {
        BarScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        List<string?> labels = ticks.Where(tick => tick.Label is not null).Select(tick => tick.Label).ToList();
        labels.Should().StartWith(["1", "2", "3", "4", "5"]);
    }

    [Fact]
    public void MarksPhraseStartsAboveBarsAndBeatsBelow()
    {
        BarScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Should().Contain(new TimeTick(0, TickLevel.Section, "1"));
        ticks.Should().Contain(new TimeTick(1, TickLevel.Major, "2"));
        ticks.Should().Contain(new TimeTick(4, TickLevel.Section, "5"));
        ticks.Should().Contain(tick => Math.Abs(tick.Time - 1.25) < Precision && tick.Level == TickLevel.Minor);
    }

    [Fact]
    public void LabelsFewerBarsWhenZoomedOut()
    {
        BarScale scale = _builder.BuildScale();
        TimeRange range = _builder.WithBarPixels(8).BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Where(tick => tick.Label is not null).Select(tick => tick.Label).Should().StartWith(["1", "5", "9"]);
        ticks.Should().NotContain(tick => tick.Level == TickLevel.Minor);
    }

    [Fact]
    public void NeverDrawsBeforeTheFirstBar()
    {
        BarScale scale = _builder.BuildScale();
        TimeRange range = new();
        range.Resize(640);
        range.Show(-4, 6);
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Should().NotContain(tick => tick.Time < 0);
    }

    [Fact]
    public void SnapsToBeatsWhenTheyAreDrawnAndToBarsWhenNot()
    {
        BarScale scale = _builder.BuildScale();
        TimeRange near = _builder.BuildRange();
        TimeRange far = _builder.WithBarPixels(8).BuildRange();

        scale.Snap(1.3, near).Should().BeApproximately(1.25, Precision);
        scale.Snap(1.3, far).Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void FormatsBarBeatAndSixteenthFromOne()
    {
        BarScale scale = _builder.BuildScale();

        scale.Format(0).Should().Be("1.1.1");
        scale.Format(11.3125).Should().Be("12.2.2");
    }

    [Fact]
    public void DoesNotWrap()
    {
        BarScale scale = _builder.BuildScale();

        scale.Period.Should().BeNull();
        scale.Unit.Should().Be("bars");
    }
}
