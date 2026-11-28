using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Controls.Mix;
using FlickerVoid.Editor.Controls.Timelines;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Tests.Music;

public sealed class MusicEditorViewTests
{
    private const double Precision = 1e-6;

    private readonly MusicEditorViewTestsBuilder _builder = new();

    [Fact]
    public async Task ZoomsTheBarsAboutThePointerWithTheWheel()
    {
        await _builder.Run(() =>
        {
            Point at = new(6 * MusicLayout.DefaultBarWidth, 200);
            Point seen = _builder.Lanes.TranslatePoint(at, _builder.Scroll)!.Value;

            _builder.Wheel(_builder.Lanes, at, 1);

            _builder.Editor.BarWidth.Should().BeApproximately(MusicLayout.DefaultBarWidth * TimeRange.ZoomPerStep, Precision);
            double x = seen.X + _builder.Scroll.Offset.X - _builder.Lanes.Bounds.X;
            double bars = x / _builder.Editor.BarWidth;
            bars.Should().BeApproximately(6, 0.01, "the bar under the pointer stays under it");
        });
    }

    [Fact]
    public async Task ScrollsOverTheHeadersInsteadOfZooming()
    {
        await _builder.Run(() =>
        {
            Border header = _builder.Headers()[0];

            _builder.Wheel(header, new Point(60, 40), 1);

            _builder.Editor.BarWidth.Should().Be(MusicLayout.DefaultBarWidth);
        });
    }

    [Fact]
    public async Task MakesTheLanesAndTheirHeadersTallerWithControlAndTheWheel()
    {
        await _builder.Run(() =>
        {
            _builder.Wheel(_builder.Lanes, new Point(100, 200), 2, RawInputModifiers.Control);

            const double Tall = 111;
            _builder.Editor.LaneHeight.Should().Be(Tall);
            _builder.Editor.BarWidth.Should().Be(MusicLayout.DefaultBarWidth);
            Border header = _builder.Headers()[1];
            Point top = header.TranslatePoint(new Point(0, 0), _builder.Lanes)!.Value;
            header.Bounds.Height.Should().Be(Tall);
            top.Y.Should().Be(_builder.Lanes.TracksTop + Tall, "the headers keep in line with their lanes");
            _builder.Lanes.Bounds.Height.Should().BeGreaterThanOrEqualTo(_builder.Lanes.TracksTop + (4 * Tall));
        });
    }

    [Fact]
    public async Task KeepsAShortLanesNameAndGainButNotItsChips()
    {
        await _builder.Run(() =>
        {
            _builder.Wheel(_builder.Lanes, new Point(100, 200), -6, RawInputModifiers.Control);

            Border header = _builder.Headers()[0];
            header.Bounds.Height.Should().Be(MusicEditorViewModel.MinLaneHeight);
            Border chip = header.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("chip"));
            chip.IsEffectivelyVisible.Should().BeFalse();
            GainBar gain = header.GetVisualDescendants().OfType<GainBar>().First();
            Point bottom = gain.TranslatePoint(new Point(0, gain.Bounds.Height), header)!.Value;
            bottom.Y.Should().BeLessThanOrEqualTo(header.Bounds.Height);
        });
    }

    [Fact]
    public async Task PansWithShiftAndKeepsTheHeadersAtTheLeft()
    {
        await _builder.Run(() =>
        {
            _builder.Editor.BarWidth = 100;
            MusicEditorViewTestsBuilder.Settle();

            _builder.Wheel(_builder.Lanes, new Point(600, 200), -2, RawInputModifiers.Shift);

            _builder.Scroll.Offset.X.Should().BeApproximately(2 * TimelineInput.PanPerNotch, Precision);
            StackPanel headers = _builder.Find<StackPanel>("Headers");
            Point left = headers.TranslatePoint(new Point(0, 0), _builder.Scroll)!.Value;
            left.X.Should().BeApproximately(0, 0.01);
        });
    }

    [Fact]
    public async Task DragsTheDocksHeightByItsTopLine()
    {
        await _builder.Run(() =>
        {
            HeightGrip grip = _builder.Find<HeightGrip>("DockGrip");

            _builder.Drag(grip, new Point(400, 3), new Point(400, -47));

            _builder.Editor.DockHeight.Should().Be(MusicEditorViewModel.DefaultDockHeight + 50);
            _builder.Find<Border>("Dock").Bounds.Height.Should().Be(MusicEditorViewModel.DefaultDockHeight + 50);
        });
    }

    [Fact]
    public async Task FadesTheStripsOfLayersNotHeardAndNotTheOthers()
    {
        await _builder.Run(() =>
        {
            _builder.Editor.Tracks[0].IsSounding = true;
            MusicEditorViewTestsBuilder.Settle();

            List<ChannelStrip> strips = _builder.View.GetVisualDescendants().OfType<ChannelStrip>().ToList();
            strips[0].Opacity.Should().Be(1);
            strips[1].Opacity.Should().Be(0.55);
        });
    }
}
