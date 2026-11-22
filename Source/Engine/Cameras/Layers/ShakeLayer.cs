using System.Numerics;

using FlickerVoid.Engine.Cameras.Directing;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>
/// Shakes the camera with smooth noise when a <see cref="CameraShakeCommand"/> asks, fading to nothing over the shake's duration,
/// never further than its amplitudes; a constant strength shakes it all the time, like a hand-held camera.
/// </summary>
public sealed class ShakeLayer : CameraLayer<ShakeState>
{
    /// <summary>Metres the camera moves at full strength, across and up.</summary>
    public float PositionAmplitude { get; set; } = 0.08f;

    /// <summary>Degrees the camera turns at full strength, on each axis.</summary>
    public float RotationAmplitude { get; set; } = 0.6f;

    /// <summary>Shakes a second, roughly.</summary>
    public float Frequency { get; set; } = 9;

    /// <summary>How sharply a shake fades: 1 fades evenly, more fades fast and then lingers.</summary>
    public float Falloff { get; set; } = 2;

    /// <summary>The strength it always shakes with, from 0 to 1.</summary>
    public float Constant { get; set; }

    protected override CameraOffset Update(ShakeState state, CameraLayerFrame frame)
    {
        foreach (CameraShakeCommand shake in frame.Shakes)
        {
            float strength = Math.Clamp(shake.Strength, 0, 1);
            if (strength >= Envelope(state) && shake.Duration > 0)
            {
                state.Strength = strength;
                state.Duration = shake.Duration;
                state.TimeLeft = shake.Duration;
            }
        }

        state.TimeLeft = MathF.Max(state.TimeLeft - frame.Seconds, 0);
        state.Time += frame.Seconds;
        float constant = Math.Clamp(Constant, 0, 1);
        float envelope = Envelope(state);
        float amount = MathF.Min(envelope + constant, 1);
        if (amount <= 0)
        {
            return CameraOffset.None;
        }

        float phase = state.Time * Frequency * MathF.Tau;
        Vector3 position = new(Noise(phase, 0), Noise(phase, 1), 0);
        Vector3 rotation = new(Noise(phase, 2), Noise(phase, 3), Noise(phase, 4));
        return new CameraOffset(position * (PositionAmplitude * amount), rotation * (RotationAmplitude * amount), 0);
    }

    private float Envelope(ShakeState state)
    {
        if (state.TimeLeft <= 0 || state.Duration <= 0)
        {
            return 0;
        }

        float left = state.TimeLeft / state.Duration;
        return state.Strength * MathF.Pow(left, Falloff);
    }

    /// <summary>Smooth noise from -1 to 1: two sines of unrelated frequencies, offset per channel.</summary>
    private static float Noise(float phase, int channel)
    {
        float offset = channel * 1.7f;
        float first = MathF.Sin(phase + offset);
        float second = MathF.Sin((phase * 2.31f) + (offset * 3.1f));
        return (first + second) * 0.5f;
    }
}
