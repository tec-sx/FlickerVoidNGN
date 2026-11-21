using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Core.Tests.Flows;

public sealed class FlowCanvasViewTests
{
    private const double Precision = 1e-9;

    [Fact]
    public void PansByScreenPixels()
    {
        FlowCanvasView view = new();

        view.Pan(30, -20);

        FlowPoint canvas = view.ToCanvas(30, -20);
        canvas.X.Should().BeApproximately(0, Precision);
        canvas.Y.Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void ZoomsAboutThePointUnderTheMouse()
    {
        FlowCanvasView view = new();
        view.Pan(100, 50);
        FlowPoint before = view.ToCanvas(400, 300);

        view.ZoomAt(400, 300, 2);

        view.Zoom.Should().BeGreaterThan(1);
        FlowPoint after = view.ToCanvas(400, 300);
        after.X.Should().BeApproximately(before.X, Precision);
        after.Y.Should().BeApproximately(before.Y, Precision);
    }

    [Fact]
    public void KeepsTheZoomWithinItsLimits()
    {
        FlowCanvasView view = new();

        view.ZoomAt(0, 0, -100);

        view.Zoom.Should().Be(FlowCanvasView.MinZoom);
    }

    [Fact]
    public void FramesABoxWholeAndCentred()
    {
        FlowCanvasView view = new();

        view.Frame(-2000, 500, 2000, 900, 1000, 600);

        FlowPoint left = view.ToScreen(-2000, 700);
        FlowPoint right = view.ToScreen(2000, 700);
        left.X.Should().BeGreaterThanOrEqualTo(0);
        right.X.Should().BeLessThanOrEqualTo(1000);
        ((left.X + right.X) / 2).Should().BeApproximately(500, 1e-6);
        left.Y.Should().BeApproximately(300, 1e-6);
    }

    [Fact]
    public void DoesNotZoomInPastOneToFrameASmallBox()
    {
        FlowCanvasView view = new();

        view.Frame(0, 0, 180, 80, 1000, 600);

        view.Zoom.Should().Be(1);
    }
}
