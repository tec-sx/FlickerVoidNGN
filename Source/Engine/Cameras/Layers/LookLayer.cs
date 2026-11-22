using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>Drifts the camera across its view towards the pointer, within the radius, and settles it back when the pointer rests near the centre.</summary>
public sealed class LookLayer : CameraLayer<LayerOffset>
{
    /// <summary>Metres the camera may drift.</summary>
    public float Radius { get; set; } = 0.4f;

    /// <summary>How far from the centre, as a fraction of the half screen, the pointer moves nothing.</summary>
    public float Deadzone { get; set; } = 0.25f;

    /// <summary>Seconds the camera takes to come most of the way out.</summary>
    public float Damping { get; set; } = 0.6f;

    /// <summary>Seconds the camera takes to come most of the way back.</summary>
    public float ReturnTime { get; set; } = 1;

    protected override CameraOffset Update(LayerOffset state, CameraLayerFrame frame)
    {
        Vector3 desired = Vector3.Zero;
        if (frame.Pointer is { } pointer)
        {
            float length = MathF.Min(pointer.Length(), 1);
            if (length > Deadzone && Deadzone < 1)
            {
                float reach = (length - Deadzone) / (1 - Deadzone);
                Vector2 direction = Vector2.Normalize(pointer);
                desired = new Vector3(direction * (reach * Radius), 0);
            }
        }

        float time = Damping;
        if (desired.LengthSquared() < state.Offset.LengthSquared())
        {
            time = ReturnTime;
        }

        Vector3 offset = Smoothing.Towards(state.Offset, desired, time, frame.Seconds);
        state.Offset = Smoothing.Limit(offset, Radius);
        return new CameraOffset(state.Offset, Vector3.Zero, 0);
    }
}
