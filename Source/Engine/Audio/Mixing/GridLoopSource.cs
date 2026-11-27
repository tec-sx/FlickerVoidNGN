namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>
/// Loops a clip on a grid: every period of mixer frames it starts over, whatever the clip's own length (a longer clip is cut, a
/// shorter one followed by silence), beginning at an offset into the period, so loops started at different times stay in step.
/// </summary>
public sealed class GridLoopSource : ISoundSource
{
    private readonly SoundClip _clip;
    private readonly double _period;
    private readonly double _rate;
    private double _position;

    public GridLoopSource(SoundClip clip, double periodFrames, double offsetFrames = 0)
    {
        if (periodFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(periodFrames), periodFrames, "A loop's period is at least a frame.");
        }

        _clip = clip;
        _period = periodFrames;
        _rate = clip.SampleRate / (double)AudioFormat.SampleRate;
        _position = offsetFrames % periodFrames;
    }

    public int Read(Span<float> stereo)
    {
        int frames = stereo.Length / AudioFormat.Channels;
        int length = _clip.Frames;
        for (int frame = 0; frame < frames; frame++)
        {
            if (_position >= _period)
            {
                _position -= _period;
            }

            double at = _position * _rate;
            int index = (int)at;
            float left = 0;
            float right = 0;
            if (index < length)
            {
                float blend = (float)(at - index);
                int next = Math.Min(index + 1, length - 1);
                left = Sample(index, 0, next, blend);
                right = _clip.Channels == 1 ? left : Sample(index, 1, next, blend);
            }

            stereo[frame * 2] = left;
            stereo[(frame * 2) + 1] = right;
            _position++;
        }

        return frames;
    }

    private float Sample(int index, int channel, int next, float blend)
    {
        float[] samples = _clip.Samples;
        float here = samples[(index * _clip.Channels) + channel];
        float there = samples[(next * _clip.Channels) + channel];
        return here + ((there - here) * blend);
    }
}
