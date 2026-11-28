using System.Runtime;

using FlickerVoid.Engine.Audio;
using FlickerVoid.Engine.Audio.Devices;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>
/// A small game of its own with the audio the music editor listens through: the cue being edited plays on its music system, and
/// the project's sound files are read through the content manager. It plays to the system's output device, or to SDL's dummy
/// one, or to nowhere, and then the time still runs with <see cref="Tick"/>.
/// </summary>
public sealed class MusicPreview : IDisposable
{
    /// <summary>The device's buffer unless another is chosen, 21 ms: twice a game's, and the editor's top bar offers longer ones.</summary>
    public const int DefaultBufferFrames = 1024;

    /// <summary>How far ahead the music is scheduled, 250 ms: past the longest buffer and any stall of the editor's window.</summary>
    public const int LookaheadFrames = 12000;

    private readonly string? _driver;
    private readonly GCLatencyMode _latency = GCSettings.LatencyMode;
    private readonly Game _game = new(NullLoggerFactory.Instance);
    private SdlAudioDevice? _device;
    private readonly NullAudioDevice? _silent;
    private readonly MusicSystem _music;
    private readonly AudioSystem _audio;

    public MusicPreview(IContentManager content, int bufferFrames = DefaultBufferFrames)
    {
        _game.Services.AddService(content);
        DataAssets data = new(content, _game.Data);
        _game.Services.AddService(data);
        _audio = _game.AddAudio();
        _audio.SteamAudio = null;
        _music = _game.Services.GetService<MusicSystem>()!;
        _music.IgnoreConditions = true;
        _music.Lookahead = LookaheadFrames;
        _device = SdlAudioDevice.TryOpen(_audio.Mixer, out string problem, null, bufferFrames);
        if (_device is null)
        {
            _device = SdlAudioDevice.TryOpen(_audio.Mixer, out problem, "dummy", bufferFrames);
            _driver = "dummy";
            Problem = "No sound device: the music plays to nowhere.";
        }

        if (_device is null)
        {
            _silent = new NullAudioDevice(_audio.Mixer);
            Problem = $"No sound device ({problem}).";
        }

        // The audio thread runs the mixer's managed code, which a blocking collection stops: fewer of those while this listens.
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        _game.Lifecycle.Play(_game.World);
    }

    /// <summary>The buffers the editor offers, in frames: shorter answers sooner, longer rides out a busier editor.</summary>
    public static IReadOnlyList<int> BufferOptions { get; } = [512, 1024, 2048, 4096];

    /// <summary>Opens the device again with another buffer; what plays goes on from where the mixer is.</summary>
    public void SetBuffer(int frames)
    {
        if (_device is null || _device.BufferFrames == frames)
        {
            return;
        }

        _device.Dispose();
        _device = SdlAudioDevice.TryOpen(_audio.Mixer, out _, _driver, frames);
    }

    /// <summary>The frames the open device takes at a time; 0 without one.</summary>
    public int DeviceBufferFrames => _device?.BufferFrames ?? 0;

    /// <summary>Why nothing can be heard, or empty when the output device is open.</summary>
    public string Problem { get; } = string.Empty;

    public MusicSystem Music => _music;

    public AudioSystem Audio => _audio;

    public Mixer Mixer => _audio.Mixer;

    /// <summary>Runs the game's frames for the time passed; where no device pulls the mixer, renders what it would have played.</summary>
    /// <summary>Sets the time of day the preview's conditions read, in hours; it does not run on.</summary>
    public void SetHour(double hour)
    {
        _game.Clock.Speed = 0;
        _game.Clock.Set(0, hour);
    }

    public void Tick(TimeSpan elapsed)
    {
        _game.Tick(elapsed);
        _silent?.Render(elapsed);
    }

    public void Dispose()
    {
        _device?.Dispose();
        _game.Dispose();
        GCSettings.LatencyMode = _latency;
    }
}
