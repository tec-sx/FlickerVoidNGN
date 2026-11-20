using System.Numerics;

using Arch.Core;

using Avalonia.Input;

using FlickerVoid.Engine.Editor.Cameras;
using FlickerVoid.Engine.Editor.Gizmos;
using FlickerVoid.Engine.Editor.Overlays;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Tests.Viewports;

public sealed class ViewportViewModelTests : IDisposable
{
    private const int Width = ViewportViewModelTestsBuilder.Width;
    private const int Height = ViewportViewModelTestsBuilder.Height;

    private readonly ViewportViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData(Key.Q, GizmoMode.Select)]
    [InlineData(Key.W, GizmoMode.Move)]
    [InlineData(Key.E, GizmoMode.Rotate)]
    [InlineData(Key.R, GizmoMode.Scale)]
    public void SwitchesGizmoModeWithQwer(Key key, GizmoMode mode)
    {
        _builder.ViewModel.GizmoMode = GizmoMode.Select;
        if (mode == GizmoMode.Select)
        {
            _builder.ViewModel.GizmoMode = GizmoMode.Move;
        }

        _builder.ViewModel.KeyPressed(key, ViewportButtons.None);

        _builder.ViewModel.GizmoMode.Should().Be(mode);
    }

    [Fact]
    public void LeavesQwerToTheCameraWhileTheRightButtonIsHeld()
    {
        _builder.ViewModel.GizmoMode = GizmoMode.Select;

        _builder.ViewModel.KeyPressed(Key.W, ViewportButtons.Right);

        _builder.ViewModel.GizmoMode.Should().Be(GizmoMode.Select);
    }

    [Fact]
    public void SelectsWhatIsClicked()
    {
        Entity light = _builder.AddPointLight(new Vector3(1, 0.5f, 0));
        Vector2 pixel = _builder.PixelOf(new Vector3(1, 0.5f, 0));

        _builder.Click(pixel);

        _builder.ViewModel.Selection.Selected.Should().Be(light);
    }

    [Fact]
    public void ClearsTheSelectionWhenNothingIsClicked()
    {
        _builder.AddPointLight(new Vector3(1, 0.5f, 0));
        Vector2 pixel = _builder.PixelOf(new Vector3(1, 0.5f, 0));
        _builder.Click(pixel);

        _builder.Click(new Vector2(5, 5));

        _builder.ViewModel.Selection.Selected.Should().BeNull();
    }

    [Fact]
    public void MovesTheSelectionWithTheGizmo()
    {
        Entity light = _builder.AddPointLight(Vector3.Zero);
        Vector2 centre = _builder.PixelOf(Vector3.Zero);
        _builder.Click(centre);
        _builder.ViewModel.GizmoMode = GizmoMode.Move;
        float length = _builder.ViewModel.Gizmo.GetHandleLength(_builder.ViewModel.Camera, Vector3.Zero, Height);
        Vector2 handle = _builder.PixelOf(new Vector3(0, 0.7f * length, 0));
        Vector2 raised = _builder.PixelOf(new Vector3(0, (0.7f * length) + 2, 0));

        _builder.ViewModel.PointerPressed(handle, Width, Height);
        _builder.ViewModel.PointerMoved(raised, Width, Height);
        _builder.ViewModel.PointerReleased();

        Transform transform = _builder.World.Get<Transform>(light);
        Vector3.Distance(transform.Position, new Vector3(0, 2, 0)).Should().BeLessThan(0.02f);
        _builder.ViewModel.Selection.Selected.Should().Be(light);
    }

    [Fact]
    public void TogglesTheGameViewWithG()
    {
        _builder.ViewModel.KeyPressed(Key.G, ViewportButtons.None);

        _builder.ViewModel.IsGameView.Should().BeTrue();
        _builder.ViewModel.IsGridVisible.Should().BeFalse();

        _builder.ViewModel.KeyPressed(Key.G, ViewportButtons.None);

        _builder.ViewModel.IsGameView.Should().BeFalse();
        _builder.ViewModel.IsGridVisible.Should().BeTrue();
    }

    [Fact]
    public void DrawsNoIconsSelectionOrGizmoInTheGameView()
    {
        Entity light = _builder.AddPointLight(new Vector3(1, 0.5f, 0));
        _builder.ViewModel.Selection.Select(light);
        _builder.ViewModel.GizmoMode = GizmoMode.Move;
        _builder.ViewModel.IsGameView = true;
        OverlayBuilder overlay = new();

        _builder.ViewModel.BuildOverlay(overlay, Height);

        overlay.Icons.IsEmpty.Should().BeTrue();
        overlay.Vertices.IsEmpty.Should().BeTrue();
    }
}
