using System.Numerics;

using Avalonia.Input;

namespace FlickerVoid.Engine.Editor.Cameras;

/// <summary>
/// Moves the editor camera as Unreal's viewport does. With Alt: the left button orbits, the middle button pans and the right button
/// zooms. Without Alt, the right button looks around, flies with WASD (Q down, E up) and the wheel sets the flying speed; otherwise
/// the wheel zooms. The movement keys do nothing without the right button, so Q, W, E and R stay free for the gizmos.
/// </summary>
public sealed class EditorCameraController(EditorCamera camera)
{
    private const float RadiansPerPixel = 0.005f;
    private const float DollyStepsPerPixel = 0.02f;
    private const float SpeedPerNotch = 1.25f;
    private const float MinFlySpeed = 0.1f;
    private const float MaxFlySpeed = 200;

    public EditorCamera Camera { get; } = camera;

    public void Apply(in ViewportInput input, float seconds, int viewportHeight)
    {
        Vector2 delta = input.MouseDelta;
        if (input.Alt)
        {
            ApplyAlt(input, delta, viewportHeight);
            return;
        }

        if (input.IsHeld(ViewportButtons.Right))
        {
            Camera.Turn(-delta.X * RadiansPerPixel, -delta.Y * RadiansPerPixel);
            Vector3 direction = GetFlyDirection(input);
            Camera.Fly(direction, seconds);
            float speed = Camera.FlySpeed * MathF.Pow(SpeedPerNotch, input.Wheel);
            Camera.FlySpeed = Math.Clamp(speed, MinFlySpeed, MaxFlySpeed);
            return;
        }

        Camera.Dolly(input.Wheel);
    }

    private static Vector3 GetFlyDirection(in ViewportInput input)
    {
        float right = Axis(input, Key.D, Key.A);
        float up = Axis(input, Key.E, Key.Q);
        float forward = Axis(input, Key.W, Key.S);
        return new Vector3(right, up, forward);
    }

    private static float Axis(in ViewportInput input, Key positive, Key negative)
    {
        float value = 0;
        if (input.IsHeld(positive))
        {
            value += 1;
        }

        if (input.IsHeld(negative))
        {
            value -= 1;
        }

        return value;
    }

    private void ApplyAlt(in ViewportInput input, Vector2 delta, int viewportHeight)
    {
        if (input.IsHeld(ViewportButtons.Left))
        {
            Camera.Orbit(-delta.X * RadiansPerPixel, -delta.Y * RadiansPerPixel);
        }
        else if (input.IsHeld(ViewportButtons.Middle))
        {
            Camera.Pan(delta, viewportHeight);
        }
        else if (input.IsHeld(ViewportButtons.Right))
        {
            Camera.Dolly((delta.X - delta.Y) * DollyStepsPerPixel);
        }

        Camera.Dolly(input.Wheel);
    }
}
