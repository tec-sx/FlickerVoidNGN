using Avalonia;
using Avalonia.Input;

using FlickerVoid.Editor.Controls.Timelines;
using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Tests.Controls.Timelines;

public sealed class TimelineControlsTests
{
    private const double Precision = 1e-6;

    private readonly TimelineControlsTestsBuilder _builder = new();

    [Fact]
    public async Task DrawsTheRulerAndLanesAtTheDesignsMeasures()
    {
        await _builder.Run(() =>
        {
            _builder.Ruler.Bounds.Height.Should().Be(24);
            _builder.Lanes.Bounds.Height.Should().Be(TimelineLane.DefaultHeight + TimelineLane.SubHeight, "the lanes are as tall as their rows");
            _builder.Timeline.Range.Width.Should().Be(640, "the controls give the shared range their width");
        });
    }

    [Fact]
    public async Task ScrubsTheCursorFromTheRuler()
    {
        await _builder.Run(() =>
        {
            _builder.Click(_builder.Ruler, new Point(130, 12));

            _builder.Timeline.Cursor.Should().BeApproximately(3, Precision, "130 px is bar 3.25 on 40 px a bar, snapped to the bar as beats are too close to draw");
        });
    }

    [Fact]
    public async Task DrawsANewRegionAlongTheRuler()
    {
        await _builder.Run(() =>
        {
            _builder.Drag(_builder.Ruler, new Point(80, 12), new Point(240, 12));

            _builder.Editor.Changes.Should().Equal(new TimelineChange(TimelineChangeKind.CreateRegion, null, null, 2, 4));
        });
    }

    [Fact]
    public async Task MovesAClipDraggedOnItsLane()
    {
        await _builder.Run(() =>
        {
            _builder.Drag(_builder.Lanes, new Point(150, 40), new Point(190, 40));

            _builder.Clip.IsSelected.Should().BeTrue();
            _builder.Editor.Changes[^1].Should().Be(new TimelineChange(TimelineChangeKind.MoveItem, _builder.Clip, null, 3, 4));
            _builder.Editor.Completions.Should().Be(1);
        });
    }

    [Fact]
    public async Task MovesFreelyWithAltHeld()
    {
        await _builder.Run(() =>
        {
            _builder.Drag(_builder.Lanes, new Point(150, 40), new Point(155, 40), RawInputModifiers.Alt);

            _builder.Editor.Changes[^1].Start.Should().BeApproximately(2.125, Precision);
        });
    }

    [Fact]
    public async Task ZoomsWithTheWheelAboutThePointer()
    {
        await _builder.Run(() =>
        {
            _builder.Wheel(_builder.Lanes, new Point(320, 40), 2);

            _builder.Timeline.Range.Span.Should().BeLessThan(16);
            _builder.Timeline.Range.ToTime(320).Should().BeApproximately(8, Precision);
        });
    }

    [Fact]
    public async Task PansWithShiftAndTheWheel()
    {
        await _builder.Run(() =>
        {
            _builder.Wheel(_builder.Lanes, new Point(320, 40), -1, RawInputModifiers.Shift);

            _builder.Timeline.Range.Span.Should().BeApproximately(16, Precision);
            _builder.Timeline.Range.MinTime.Should().BeApproximately(TimelineInput.PanPerNotch / 40, Precision);
        });
    }

    [Theory]
    [InlineData(RawInputModifiers.Control)]
    [InlineData(RawInputModifiers.Alt)]
    public async Task LeavesTheWheelWithControlOrAltToTheControlAround(RawInputModifiers modifiers)
    {
        await _builder.Run(() =>
        {
            _builder.Wheel(_builder.Lanes, new Point(320, 40), 2, modifiers);

            _builder.Timeline.Range.Span.Should().BeApproximately(16, Precision);
            _builder.Timeline.Range.MinTime.Should().Be(0);
        });
    }

    [Theory]
    [InlineData(KeyModifiers.None, 0, 1, TimelineWheel.ZoomTime)]
    [InlineData(KeyModifiers.Control, 0, 1, TimelineWheel.ScaleLanes)]
    [InlineData(KeyModifiers.Shift, 0, 1, TimelineWheel.Pan)]
    [InlineData(KeyModifiers.Alt, 0, 1, TimelineWheel.Scroll)]
    [InlineData(KeyModifiers.None, 1, 0.2, TimelineWheel.Pan)]
    public void ReadsTheWheelAlikeOnEveryTimeline(KeyModifiers keys, double sideways, double down, TimelineWheel expected)
    {
        Vector delta = new(sideways, down);

        TimelineWheel wheel = TimelineInput.Read(keys, delta);

        wheel.Should().Be(expected);
    }

    [Fact]
    public async Task LetsThePointerThroughTheOverlay()
    {
        await _builder.Run(() =>
        {
            _builder.Overlay.IsHitTestVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task DrawsItsKindsWithTheKitsPainters()
    {
        await _builder.Run(() =>
        {
            TimelinePainters.Find(TimelineKinds.Clip).Should().BeOfType<ClipPainter>();
            TimelinePainters.Find(TimelineKinds.Fill).Should().BeOfType<FillPainter>();
            TimelinePainters.Find(TimelineKinds.Key).Should().BeOfType<KeyPainter>();
            TimelinePainters.Find(TimelineKinds.Marker).Should().BeOfType<MarkerPainter>();
            TimelinePainters.Find("unknown").Should().BeOfType<ClipPainter>("a kind nobody registered is drawn as a clip");
        });
    }
}
