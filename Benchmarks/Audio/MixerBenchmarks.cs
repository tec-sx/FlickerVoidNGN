using BenchmarkDotNet.Attributes;

using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Benchmarks.Audio;

/// <summary>
/// Mixes a second of sound, as the audio thread does in blocks: loops on the music grid, each through its voice's filter, its bus
/// and the master's limiter, with the reverb on. A second of mixing that takes 10 ms is 1% of the audio thread's time.
/// </summary>
[MemoryDiagnoser]
public class MixerBenchmarks
{
    private const int ClipFrames = AudioFormat.SampleRate * 2;

    private readonly float[] _second = new float[AudioFormat.SampleRate * AudioFormat.Channels];
    private Mixer _mixer = null!;

    [Params(3, 16, 64)]
    public int Voices { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _mixer = new Mixer();
        Random random = new(7);
        float[] samples = new float[ClipFrames * AudioFormat.Channels];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = ((float)random.NextDouble() - 0.5f) * 0.5f;
        }

        SoundClip clip = new(samples, AudioFormat.Channels, AudioFormat.SampleRate);
        for (int voice = 0; voice < Voices; voice++)
        {
            AudioBus bus = voice % 2 == 0 ? AudioBus.Music : AudioBus.Ambience;
            GridLoopSource loop = new(clip, ClipFrames, voice * 100);
            MixerCommand play = MixerCommand.PlayOnGrid(voice + 1, loop, bus, 0.5f, 0, 0);
            _mixer.Send(in play);
        }

        MixerCommand reverb = MixerCommand.SetReverb(0.3f, 0.6f, 0.4f);
        _mixer.Send(in reverb);
    }

    [Benchmark]
    public void MixASecond() => _mixer.Render(_second);
}
