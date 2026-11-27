using FlickerVoid.Engine.Audio.Mixing;
using NVorbis;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Decodes Ogg Vorbis files whole, through NVorbis; files of more than two channels keep their first two.</summary>
public static class OggReader
{
    public static SoundClip Read(Stream file)
    {
        using VorbisReader reader = new(file, closeOnDispose: false);
        int channels = reader.Channels;
        int kept = Math.Min(channels, 2);
        List<float> samples = [];
        float[] buffer = new float[4096 * channels];
        while (true)
        {
            int read = reader.ReadSamples(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            for (int index = 0; index + channels <= read; index += channels)
            {
                for (int channel = 0; channel < kept; channel++)
                {
                    float sample = Math.Clamp(buffer[index + channel], -1, 1);
                    samples.Add(sample);
                }
            }
        }

        return new SoundClip([.. samples], kept, reader.SampleRate);
    }
}
