using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>Leans the camera across its view after the shot's target, never further than the radius from the rail point.</summary>
public sealed class FollowLayer : CameraLayer<LayerOffset>
{
    /// <summary>How much of the target's offset from the centre of the view the camera follows, from 0 to 1.</summary>
    public float Strength { get; set; } = 0.3f;

    /// <summary>Metres the camera may lean.</summary>
    public float Radius { get; set; } = 0.5f;

    /// <summary>Seconds the camera takes to come most of the way.</summary>
    public float Damping { get; set; } = 0.5f;

    protected override CameraOffset Update(LayerOffset state, CameraLayerFrame frame)
    {
        Vector3 desired = Vector3.Zero;
        if (frame.Target is { } target)
        {
            Vector3 toTarget = target - frame.View.Transform.Position;
            Quaternion inverse = Quaternion.Inverse(frame.View.Transform.Rotation);
            Vector3 local = Vector3.Transform(toTarget, inverse);
            Vector3 across = new(local.X * Strength, local.Y * Strength, 0);
            desired = Smoothing.Limit(across, Radius);
        }

        Vector3 offset = Smoothing.Towards(state.Offset, desired, Damping, frame.Seconds);
        state.Offset = Smoothing.Limit(offset, Radius);
        return new CameraOffset(state.Offset, Vector3.Zero, 0);
    }
}
