using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

public sealed class HourScaleTests
{
    private const double Precision = 1e-6;

    private readonly HourScaleTestsBuilder _builder = new();

    [Fact]
    public void TicksEveryHourAndLabelsEveryThreeOnADockWideDay()
    {
        HourScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Should().HaveCount(25);
        ticks.Where(tick => tick.Label is not null).Select(tick => tick.Label).Should().Equal(
            "00:00", "03:00", "06:00", "09:00", "12:00", "15:00", "18:00", "21:00", "00:00");
    }

    [Fact]
    public void LabelsEveryHourWhenZoomedIn()
    {
        HourScale scale = _builder.BuildScale();
        TimeRange range = _builder.WithHourPixels(120).BuildRange();
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Should().Contain(new TimeTick(1, TickLevel.Major, "01:00"));
    }

    [Fact]
    public void WrapsTheLabelsPastMidnight()
    {
        HourScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();
        range.Resize(1440);
        range.Show(-6, 30);
        List<TimeTick> ticks = [];

        scale.GetTicks(range, ticks);

        ticks.Should().Contain(new TimeTick(-3, TickLevel.Major, "21:00"));
        ticks.Should().Contain(new TimeTick(24, TickLevel.Section, "00:00"));
    }

    [Fact]
    public void SnapsToQuarterHours()
    {
        HourScale scale = _builder.BuildScale();
        TimeRange range = _builder.BuildRange();

        scale.Snap(6.1, range).Should().BeApproximately(6, Precision);
        scale.Snap(6.2, range).Should().BeApproximately(6.25, Precision);
    }

    [Fact]
    public void WrapsAroundTheDay()
    {
        HourScale scale = _builder.BuildScale();

        scale.Period.Should().Be(24);
        scale.Wrap(25.5).Should().BeApproximately(1.5, Precision);
        scale.Wrap(-1).Should().BeApproximately(23, Precision);
    }

    [Fact]
    public void FormatsHoursAndMinutes()
    {
        HourScale scale = _builder.BuildScale();

        scale.Format(18.5).Should().Be("18:30");
        scale.Format(24.25).Should().Be("00:15");
    }
}
