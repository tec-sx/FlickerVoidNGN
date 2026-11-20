using System.Numerics;

using Avalonia.Input;

namespace FlickerVoid.Engine.Editor.Cameras;

/// <summary>One frame of input over the viewport: what is held, how far the mouse moved in pixels (y down) and how many notches the wheel turned.</summary>
public readonly record struct ViewportInput(ViewportButtons Buttons, bool Alt, IReadOnlySet<Key> Keys, Vector2 MouseDelta, float Wheel)
{
    public bool IsHeld(ViewportButtons button) => (Buttons & button) != 0;

    public bool IsHeld(Key key) => Keys.Contains(key);
}
