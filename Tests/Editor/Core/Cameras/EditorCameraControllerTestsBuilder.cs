using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;

namespace FlickerVoid.Editor.Core.Tests.Cameras;

/// <summary>Builds a controller for a camera looking at the origin from 10 units, and one frame of input for it in a 500 pixel high viewport.</summary>
internal sealed class EditorCameraControllerTestsBuilder
{
    public const int ViewportHeight = 500;

    private readonly HashSet<ViewportKey> _keys = [];
    private ViewportButtons _buttons;
    private bool _alt;
    private Vector2 _mouseDelta;
    private float _wheel;

    public EditorCameraControllerTestsBuilder()
    {
        Camera.LookAt(Vector3.Zero, 0.3f, -0.5f, 10);
        Controller = new EditorCameraController(Camera);
    }

    public EditorCamera Camera { get; } = new();

    public EditorCameraController Controller { get; }

    public EditorCameraControllerTestsBuilder Holding(ViewportButtons buttons)
    {
        _buttons = buttons;
        return this;
    }

    public EditorCameraControllerTestsBuilder HoldingAlt()
    {
        _alt = true;
        return this;
    }

    public EditorCameraControllerTestsBuilder HoldingKey(ViewportKey key)
    {
        _keys.Add(key);
        return this;
    }

    public EditorCameraControllerTestsBuilder Dragging(float x, float y)
    {
        _mouseDelta = new Vector2(x, y);
        return this;
    }

    public EditorCameraControllerTestsBuilder Scrolling(float notches)
    {
        _wheel = notches;
        return this;
    }

    /// <summary>Applies the input to the camera as one frame of the given length.</summary>
    public void Apply(float seconds = 0.1f)
    {
        ViewportInput input = new(_buttons, _alt, _keys, _mouseDelta, _wheel);
        Controller.Apply(input, seconds, ViewportHeight);
    }
}
