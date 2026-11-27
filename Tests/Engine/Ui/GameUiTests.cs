using System.Numerics;

using Avalonia.Media;

namespace FlickerVoid.Engine.Ui.Tests;

public sealed class GameUiTests : IDisposable
{
    private readonly GameUiTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsItsControlsOverTheSceneAndLeavesTheSceneElsewhere()
    {
        uint[] pixels = _builder.RenderFrame();

        GameUiTestsBuilder.At(pixels, 10, 10).Should().Be(0xFF0000FF, "the red panel covers the top left");
        GameUiTestsBuilder.At(pixels, 160, 100).Should().Be(GameUiTestsBuilder.Scene);
    }

    [Fact]
    public void DrawsNothingWhileHidden()
    {
        _builder.Ui.Visible = false;

        uint[] pixels = _builder.RenderFrame();

        pixels.Should().OnlyContain(pixel => pixel == GameUiTestsBuilder.Scene);
    }

    [Fact]
    public void FollowsTheWindowsSize()
    {
        _builder.ResizeWindow(GameUiTestsBuilder.Width / 2, GameUiTestsBuilder.Height / 2);

        _builder.RenderFrame();

        _builder.Ui.Surface.Width.Should().Be(GameUiTestsBuilder.Width / 2);
        _builder.Ui.Surface.Height.Should().Be(GameUiTestsBuilder.Height / 2);
    }

    [Fact]
    public void CapturesThePointerOverItsControlsOnlyWhileVisible()
    {
        _builder.MoveMouse(new Vector2(20, 20));
        _builder.RenderFrame();
        bool shown = _builder.Ui.CapturesPointer;

        _builder.Ui.Visible = false;
        _builder.RenderFrame();
        bool hidden = _builder.Ui.CapturesPointer;

        shown.Should().BeTrue();
        hidden.Should().BeFalse();
    }

    [Fact]
    public void KeepsShowingItsControlsOnFramesWhereNothingChanged()
    {
        _builder.RenderFrame();

        uint[] second = _builder.RenderFrame();
        uint[] third = _builder.RenderFrame();
        uint[] fourth = _builder.RenderFrame();

        GameUiTestsBuilder.At(second, 10, 10).Should().Be(0xFF0000FF);
        GameUiTestsBuilder.At(third, 10, 10).Should().Be(0xFF0000FF);
        GameUiTestsBuilder.At(fourth, 10, 10).Should().Be(0xFF0000FF);
    }

    [Fact]
    public void ShowsAChangeOnTheNextFrame()
    {
        _builder.RenderFrame();
        _builder.RenderFrame();

        _builder.Panel.Background = Brushes.Lime;
        uint[] pixels = _builder.RenderFrame();

        GameUiTestsBuilder.At(pixels, 10, 10).Should().Be(0xFF00FF00);
    }
}
