namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>A one-pole low-pass filter over interleaved stereo, keeping its state between blocks.</summary>
public struct LowPass
{
    private float _left;
    private float _right;

    /// <summary>Filters the block in place at the cutoff in hertz; at or above <see cref="MixerCommand.NoCutoff"/> it only follows the signal, so a later cutoff starts without a click.</summary>
    public void Process(Span<float> stereo, float cutoff)
    {
        if (cutoff >= MixerCommand.NoCutoff)
        {
            if (stereo.Length >= 2)
            {
                _left = stereo[^2];
                _right = stereo[^1];
            }

            return;
        }

        float amount = 1 - MathF.Exp(-2 * MathF.PI * cutoff / AudioFormat.SampleRate);
        for (int index = 0; index + 1 < stereo.Length; index += 2)
        {
            _left += amount * (stereo[index] - _left);
            _right += amount * (stereo[index + 1] - _right);
            stereo[index] = _left;
            stereo[index + 1] = _right;
        }
    }
}
