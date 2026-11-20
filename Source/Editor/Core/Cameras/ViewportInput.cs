using System.Numerics;

namespace FlickerVoid.Editor.Core.Cameras;

/// <summary>One frame of input over the viewport: what is held, how far the mouse moved in pixels (y down) and how many notches the wheel turned.</summary>
public readonly record struct ViewportInput(ViewportButtons Buttons, bool Alt, IReadOnlySet<ViewportKey> Keys, Vector2 MouseDelta, float Wheel)
{
    public bool IsHeld(ViewportButtons button) => (Buttons & button) != 0;

    public bool IsHeld(ViewportKey key) => Keys.Contains(key);
}
