using Arch.Core;

using System.Numerics;

using Avalonia;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Ui.Surfaces;

using FlickerVoid.Engine.Ui.Stages;

namespace FlickerVoid.Engine.Ui.Tests.Stages;

public sealed class StageTests : IDisposable
{
    private readonly StageTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void LoadsItsSceneIntoAWorldOfItsOwn()
    {
        Stage stage = _builder.Build();

        stage.World.Should().NotBeSameAs(_builder.Game.World);
        stage.World.CountEntities(new QueryDescription()).Should().Be(2);
        _builder.Game.World.CountEntities(new QueryDescription()).Should().Be(0);
    }

    [Fact]
    public void RendersTheCamerasViewIntoABitmapWhileShown()
    {
        Stage stage = _builder.Build();
        int rendered = 0;
        stage.Rendered += () => rendered++;

        stage.Update(1f / 60);
        stage.Size = new PixelSize(64, 48);
        stage.IsShown = true;
        stage.Update(1f / 60);

        rendered.Should().Be(1, "a hidden stage draws nothing");
        stage.Bitmap!.PixelSize.Should().Be(new PixelSize(64, 48));
        _builder.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TurnsItsTurntablesAsItUpdatesAndIsDragged()
    {
        Stage stage = _builder.Build();

        stage.Update(0.5f);
        stage.Drag(10);

        _builder.Yaw(stage).Should().BeApproximately(45, 0.01f);
    }

    [Fact]
    public void ItsViewShowsItAtItsSizeAndTurnsItWhenDragged()
    {
        Stage stage = _builder.Build();
        StageView view = new() { Stage = stage, Width = 80, Height = 60 };
        using UiSurface surface = new(view, 80, 60);
        InputState input = new();
        surface.Update(input);

        input.MoveMouse(new Vector2(20, 30));
        surface.Update(input);
        input.PressButton(Silk.NET.Input.MouseButton.Left);
        surface.Update(input);
        input.EndFrame();
        input.MoveMouse(new Vector2(40, 30));
        surface.Update(input);

        stage.IsShown.Should().BeTrue();
        stage.Size.Should().Be(new PixelSize(80, 60));
        _builder.Yaw(stage).Should().BeApproximately(10, 0.01f);
    }
}
