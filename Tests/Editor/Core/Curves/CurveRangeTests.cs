using FlickerVoid.Editor.Core.Curves;

namespace FlickerVoid.Editor.Core.Tests.Curves;

public sealed class CurveRangeTests
{
    private const double Precision = 1e-6;

    private readonly CurveRangeTestsBuilder _builder = new();

    [Fact]
    public void DrawsTimeAcrossAndValueUpward()
    {
        CurveRange range = _builder.Build();

        CurvePoint low = range.ToScreen(0, 0);
        CurvePoint high = range.ToScreen(24, 10);

        low.X.Should().BeApproximately(0, Precision);
        low.Y.Should().BeApproximately(100, Precision);
        high.X.Should().BeApproximately(240, Precision);
        high.Y.Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void ReadsTheTimeAndValueUnderAScreenPoint()
    {
        CurveRange range = _builder.Build();

        range.ToTime(60).Should().BeApproximately(6, Precision);
        range.ToValue(25).Should().BeApproximately(7.5, Precision);
    }

    [Fact]
    public void PansByScreenPixels()
    {
        CurveRange range = _builder.Build();

        range.Pan(20, -10);

        range.MinTime.Should().BeApproximately(-2, Precision);
        range.MaxTime.Should().BeApproximately(22, Precision);
        range.MinValue.Should().BeApproximately(-1, Precision);
        range.MaxValue.Should().BeApproximately(9, Precision);
    }

    [Fact]
    public void ZoomsAboutThePointUnderTheMouse()
    {
        CurveRange range = _builder.Build();

        range.ZoomAt(60, 25, 2, CurveAxes.Both);

        range.ToTime(60).Should().BeApproximately(6, Precision);
        range.ToValue(25).Should().BeApproximately(7.5, Precision);
        (range.MaxTime - range.MinTime).Should().BeLessThan(24);
        (range.MaxValue - range.MinValue).Should().BeLessThan(10);
    }

    [Fact]
    public void ZoomsOneAxisAlone()
    {
        CurveRange range = _builder.Build();

        range.ZoomAt(60, 25, -2, CurveAxes.Value);

        range.MinTime.Should().BeApproximately(0, Precision);
        range.MaxTime.Should().BeApproximately(24, Precision);
        (range.MaxValue - range.MinValue).Should().BeGreaterThan(10);
    }

    [Fact]
    public void FramesABoxWithRoomAboveAndBelow()
    {
        CurveRange range = _builder.Build();

        range.Frame(2, 20, 1, 3);

        range.MinTime.Should().BeApproximately(2, Precision);
        range.MaxTime.Should().BeApproximately(20, Precision);
        range.MinValue.Should().BeLessThan(1);
        range.MaxValue.Should().BeGreaterThan(3);
    }

    [Fact]
    public void FramesAFlatBoxAroundItsValue()
    {
        CurveRange range = _builder.Build();

        range.Frame(0, 24, 2, 2);

        range.MinValue.Should().BeLessThan(2);
        range.MaxValue.Should().BeGreaterThan(2);
    }
}
