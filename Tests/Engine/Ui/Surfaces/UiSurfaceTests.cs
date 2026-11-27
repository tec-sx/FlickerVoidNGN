using System.Numerics;

using Avalonia.Media;

using FlickerVoid.Engine.Ui.Surfaces;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Ui.Tests.Surfaces;

public sealed class UiSurfaceTests : IDisposable
{
    private readonly UiSurfaceTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ItsControlsAreDrawnOverTransparentPixels()
    {
        UiSurface surface = _builder.Build();

        surface.Draw();

        UiSurfaceTestsBuilder.At(surface, 10, 10).Should().Be(((byte)255, (byte)0, (byte)0, (byte)255));
        UiSurfaceTestsBuilder.At(surface, 160, 100).A.Should().Be(0);
    }

    [Fact]
    public void ClicksReachItsButtons()
    {
        UiSurface surface = _builder.Build();
        _builder.Input.MoveMouse(new Vector2(280, 180));
        surface.Update(_builder.Input);
        _builder.Input.EndFrame();

        _builder.Input.PressButton(MouseButton.Left);
        surface.Update(_builder.Input);
        _builder.Input.EndFrame();
        _builder.Input.ReleaseButton(MouseButton.Left);
        surface.Update(_builder.Input);

        _builder.Clicks.Should().Be(1);
    }

    [Fact]
    public void ItCapturesThePointerOnlyOverItsControls()
    {
        UiSurface surface = _builder.Build();

        _builder.Input.MoveMouse(new Vector2(20, 20));
        surface.Update(_builder.Input);
        bool overPanel = surface.CapturesPointer;
        _builder.Input.MoveMouse(new Vector2(160, 100));
        surface.Update(_builder.Input);
        bool overNothing = surface.CapturesPointer;

        overPanel.Should().BeTrue();
        overNothing.Should().BeFalse();
    }

    [Fact]
    public void ItFollowsTheWindowsSize()
    {
        UiSurface surface = _builder.Build();

        surface.Resize(640, 400);

        surface.Width.Should().Be(640);
        surface.Height.Should().Be(400);
        surface.Draw();
        UiSurfaceTestsBuilder.At(surface, 640 - 10, 400 - 10).A.Should().BeGreaterThan(0, "the button moved to the new bottom right corner");
    }

    [Fact]
    public void ItTellsWhetherADrawingLooksDifferentFromTheLast()
    {
        UiSurface surface = _builder.Build();

        bool first = surface.Draw();
        surface.Update(_builder.Input);
        bool same = surface.Draw();
        _builder.Panel.Background = Brushes.Lime;
        surface.Update(_builder.Input);
        bool recoloured = surface.Draw();

        first.Should().BeTrue("nothing was drawn before");
        same.Should().BeFalse("nothing changed");
        recoloured.Should().BeTrue("the panel took a new colour");
        UiSurfaceTestsBuilder.At(surface, 10, 10).Should().Be(((byte)0, (byte)255, (byte)0, (byte)255));
    }

    [Fact]
    public void ADrawingAfterAResizeLooksDifferent()
    {
        UiSurface surface = _builder.Build();
        surface.Draw();

        surface.Resize(640, 400);

        surface.Draw().Should().BeTrue();
    }
}
