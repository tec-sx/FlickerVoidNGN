using FlickerVoid.Engine.Cameras.Directing;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>Zooms quickly in (or out) and back when a <see cref="ZoomPunchCommand"/> asks, for a hit or a reveal.</summary>
public sealed class ZoomPunchLayer : CameraLayer<ZoomPunchState>
{
    /// <summary>The zoom at full strength, as a fraction of the lens's; negative zooms in.</summary>
    public float Amount { get; set; } = -0.12f;

    /// <summary>Seconds to reach the full zoom.</summary>
    public float Attack { get; set; } = 0.06f;

    /// <summary>Seconds to come back.</summary>
    public float Release { get; set; } = 0.35f;

    protected override CameraOffset Update(ZoomPunchState state, CameraLayerFrame frame)
    {
        foreach (ZoomPunchCommand punch in frame.Punches)
        {
            state.Strength = Math.Clamp(punch.Strength, 0, 1);
            state.Time = 0;
        }

        state.Time += frame.Seconds;
        float envelope = 0;
        if (state.Time < Attack)
        {
            envelope = state.Time / Attack;
        }
        else if (state.Time < Attack + Release)
        {
            envelope = 1 - ((state.Time - Attack) / Release);
        }

        return new CameraOffset(default, default, Amount * state.Strength * envelope);
    }
}
