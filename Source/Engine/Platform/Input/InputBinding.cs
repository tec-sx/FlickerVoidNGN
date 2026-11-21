using System.Numerics;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>
/// A key, a mouse button or the mouse's movement that drives an action. A key or button adds <see cref="Value"/> while it is down,
/// so W, A, S and D make a move vector; the mouse adds its movement this frame scaled by <see cref="Value"/>.
/// </summary>
public sealed class InputBinding
{
    public Key? Key { get; set; }

    public MouseButton? Button { get; set; }

    /// <summary>Binds the mouse's movement in pixels this frame.</summary>
    public bool MouseMovement { get; set; }

    public Vector2 Value { get; set; } = Vector2.UnitX;

    public bool IsDown(IInputState input)
    {
        if (Key is { } key)
        {
            return input.IsDown(key);
        }

        if (Button is { } button)
        {
            return input.IsDown(button);
        }

        return false;
    }

    public Vector2 Read(IInputState input)
    {
        if (MouseMovement)
        {
            return input.MouseDelta * Value;
        }

        if (IsDown(input))
        {
            return Value;
        }

        return Vector2.Zero;
    }
}
