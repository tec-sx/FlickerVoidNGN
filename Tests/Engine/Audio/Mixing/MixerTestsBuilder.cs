using FlickerVoid.Engine.Audio.Devices;
using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Mixing;

/// <summary>A mixer on a null device, with clips of known samples to play through it.</summary>
internal sealed class MixerTestsBuilder
{
    private int _lastVoice;

    public MixerTestsBuilder()
    {
        Device = new NullAudioDevice(Mixer);
    }

    public Mixer Mixer { get; } = new();

    public NullAudioDevice Device { get; }

    /// <summary>A mono clip at the mixer's rate holding one value throughout.</summary>
    public static SoundClip Constant(int frames, float value = 1)
    {
        float[] samples = new float[frames];
        Array.Fill(samples, value);
        return new SoundClip(samples, 1, AudioFormat.SampleRate);
    }

    /// <summary>A mono clip whose samples count up from 0 in steps of a thousandth, so a place in it can be told by its value.</summary>
    public static SoundClip Ramp(int frames)
    {
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame] = frame / 1000f;
        }

        return new SoundClip(samples, 1, AudioFormat.SampleRate);
    }

    /// <summary>A mono clip swinging between 1 and -1 every frame, the highest tone there is.</summary>
    public static SoundClip Alternating(int frames)
    {
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame] = frame % 2 == 0 ? 1 : -1;
        }

        return new SoundClip(samples, 1, AudioFormat.SampleRate);
    }

    /// <summary>Plays the clip once and returns its voice's id.</summary>
    public int Play(SoundClip clip, AudioBus bus = AudioBus.Effects, float left = 1, float right = 1, long startFrame = 0, float cutoff = MixerCommand.NoCutoff)
    {
        _lastVoice++;
        ClipSource source = new(clip);
        MixerCommand play = MixerCommand.Play(_lastVoice, source, bus, left, right, cutoff, startFrame);
        Mixer.Send(in play);
        return _lastVoice;
    }

    public List<int> TakeFinished()
    {
        List<int> finished = [];
        while (Mixer.TryTakeFinished(out int voice))
        {
            finished.Add(voice);
        }

        return finished;
    }
}
