using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Devices;

/// <summary>A device with no speakers: renders the mixer into memory when asked, so tests hear exactly what a real device would play.</summary>
public sealed class NullAudioDevice(Mixer mixer)
{
    /// <summary>Renders the next frames and returns them, interleaved stereo.</summary>
    public float[] Render(int frames)
    {
        float[] output = new float[frames * AudioFormat.Channels];
        mixer.Render(output);
        return output;
    }

    /// <summary>Renders the next stretch of time, to the nearest frame.</summary>
    public float[] Render(TimeSpan time)
    {
        double exact = time.TotalSeconds * AudioFormat.SampleRate;
        int frames = (int)Math.Round(exact);
        return Render(frames);
    }
}
