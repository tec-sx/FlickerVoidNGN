namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>Plays a clip at a pitch, once or looping, resampled to the mixer's rate by linear interpolation.</summary>
public sealed class ClipSource : ISoundSource
{
    private readonly SoundClip _clip;
    private readonly bool _loop;
    private readonly double _step;
    private double _position;

    public ClipSource(SoundClip clip, bool loop = false, float pitch = 1)
    {
        _clip = clip;
        _loop = loop;
        _step = pitch * clip.SampleRate / (double)AudioFormat.SampleRate;
    }

    public int Read(Span<float> stereo)
    {
        int frames = stereo.Length / AudioFormat.Channels;
        int length = _clip.Frames;
        if (length == 0)
        {
            return 0;
        }

        for (int frame = 0; frame < frames; frame++)
        {
            if (_position >= length)
            {
                if (!_loop)
                {
                    return frame;
                }

                _position %= length;
            }

            int index = (int)_position;
            float blend = (float)(_position - index);
            int next = index + 1;
            if (next >= length)
            {
                next = _loop ? 0 : index;
            }

            float left = Sample(index, 0, next, blend);
            float right = _clip.Channels == 1 ? left : Sample(index, 1, next, blend);
            stereo[frame * 2] = left;
            stereo[(frame * 2) + 1] = right;
            _position += _step;
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
