using Arch.Core;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Tests.Sounds;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Audio.Tests.Music;

/// <summary>
/// A playing game with audio heard through a null device, keeping every frame it played: writes cues at 480 beats a minute (a
/// bar of 24,000 frames) over loops that hold one value a bar, plays up to a bar and tells what was heard at one.
/// </summary>
internal sealed class MusicSystemTestsBuilder : IDisposable
{
    public const int BarFrames = 24000;

    private const int TickFrames = 480;

    private readonly AudioSystemTestsBuilder _audio = new();
    private readonly List<float> _heard = [];

    public Game Game => _audio.Game;

    public MusicSystem Music => _audio.Music;

    public AudioSystemTestsBuilder Audio => _audio;

    /// <summary>The frames played so far.</summary>
    public long Position => _heard.Count;

    /// <summary>Writes a mono sound file holding each value for a bar and returns its content path.</summary>
    public string WithClip(string name, params float[] bars)
    {
        float[] samples = new float[bars.Length * BarFrames];
        for (int bar = 0; bar < bars.Length; bar++)
        {
            Array.Fill(samples, bars[bar], bar * BarFrames, BarFrames);
        }

        string file = $"{name}.wav";
        byte[] wav = WavFile.Pcm16(1, 48000, samples);
        _audio.Write(file, wav);
        return file;
    }

    /// <summary>Writes a cue at 480 beats a minute with the tracks' JSON and any more settings as JSON members, and returns its content path.</summary>
    public string WithCue(string name, string tracks, string settings = "\"tempo\": 480")
    {
        string json = $$"""{ "type": "music", {{settings}}, "tracks": [ {{tracks}} ] }""";
        return _audio.WithSoundJson(name, json);
    }

    /// <summary>Runs the game a frame at a time, playing a hundredth of a second after each, until the mixer has played up to a frame.</summary>
    public void PlayUntil(long frame)
    {
        while (_heard.Count < frame)
        {
            Game.Tick(GameLoop.DefaultFixedStep);
            float[] heard = _audio.Device.Render(TickFrames);
            for (int index = 0; index < heard.Length; index += AudioFormat.Channels)
            {
                _heard.Add(heard[index]);
            }
        }
    }

    public void PlayFor(long frames) => PlayUntil(_heard.Count + frames);

    /// <summary>Plays up to a bar of the cue heard, counted from 1 with fractions; the cue must have been through a frame.</summary>
    public void PlayUntilBar(double bar)
    {
        long frame = Music.GetFrameOfBar(bar);
        PlayUntil(frame);
    }

    /// <summary>The left sample played on a frame.</summary>
    public float HeardAt(long frame) => _heard[(int)frame];

    /// <summary>The left sample played on a bar counted from the frame of a first bar.</summary>
    public float HeardAtBar(long firstBar, double bar)
    {
        double frames = (bar - 1) * BarFrames;
        return HeardAt(firstBar + (long)frames);
    }

    public Entity Spawn(string entityJson) => _audio.Spawn(entityJson);

    public void Dispose() => _audio.Dispose();
}
