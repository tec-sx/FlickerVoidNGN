using FlickerVoid.Editor.Core.Music;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class MeterBallisticsTests
{
    [Fact]
    public void RisesToAPeakAtOnce()
    {
        double shown = MeterBallistics.Follow(0.1, 0.8, TimeSpan.FromMilliseconds(16));

        shown.Should().Be(0.8);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.25)]
    public void FallsTheSameDecibelsEachMomentWhereverItIs(double start)
    {
        double shown = start;
        for (int frame = 0; frame < 15; frame++)
        {
            shown = MeterBallistics.Follow(shown, 0, TimeSpan.FromMilliseconds(250.0 / 15));
        }

        double fell = 20 * Math.Log10(start / shown);
        fell.Should().BeApproximately(12, 0.001, "a quarter of a second at 48 dB a second");
    }

    [Fact]
    public void FallsFromFullScaleToTheMetersFloorInASecond()
    {
        double half = MeterBallistics.Follow(1, 0, TimeSpan.FromSeconds(0.5));
        double whole = MeterBallistics.Follow(1, 0, TimeSpan.FromSeconds(1));

        double halfDecibels = 20 * Math.Log10(half);
        double wholeDecibels = 20 * Math.Log10(whole);
        halfDecibels.Should().BeApproximately(-24, 0.001);
        wholeDecibels.Should().BeApproximately(-48, 0.001);
    }

    [Fact]
    public void RestsAtNothingOnceItFallsUnderTheFloor()
    {
        double shown = MeterBallistics.Follow(0.0011, 0, TimeSpan.FromMilliseconds(50));

        shown.Should().Be(0);
    }
}
