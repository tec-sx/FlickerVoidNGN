using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>
/// The game's side of sound (a frame system and the <see cref="IAudioEngine"/> service): plays <c>sound</c> data assets as voices of
/// its <see cref="Mixer"/>, keeps the 3D ones panned and faded from the listener (the first <see cref="AudioListener"/>, its ears
/// turned with the camera; the camera itself without one), starts and stops <see cref="SoundEmitter"/>s with their entities, plays
/// <see cref="PlaySoundCommand"/>s, follows the <see cref="AudioSettings"/>, dulls the ambience while the listener stands in an
/// indoor volume, and holds the world's buses while the game is paused, leaving music and the interface playing.
/// </summary>
public sealed partial class AudioSystem : BaseSystem<World, GameTime>, IAudioEngine
{
    private const float GainEpsilon = 0.001f;
    private const float EmitterFadeSeconds = 0.1f;

    private static readonly QueryDescription Listeners = new QueryDescription().WithAll<AudioListener, Transform>();
    private static readonly QueryDescription Indoors = new QueryDescription().WithAll<IndoorVolume, Transform>();
    private static readonly AudioBus[] WorldBuses = [AudioBus.Ambience, AudioBus.Effects, AudioBus.Dialogue];

    private readonly Game _game;
    private readonly ILogger _logger;
    private readonly Random _random;
    private readonly Dictionary<int, Playing> _playing = [];
    private readonly Dictionary<Entity, SoundHandle> _emitters = [];
    private readonly Dictionary<string, SoundState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
    private SoundLibrary? _library;
    private Vector3 _listener;
    private Vector3 _right = Vector3.UnitX;
    private TimeSpan _now;
    private int _lastId;
    private bool _paused;
    private bool _indoors;
    private bool _busesChanged = true;

    public AudioSystem(Game game, Random? random = null)
        : base(game.World)
    {
        _game = game;
        _logger = game.LoggerFactory.CreateLogger<AudioSystem>();
        _random = random ?? new Random();
        Settings.Changed += HandleSettingsChanged;
        game.Events.Handle((in PlaySoundCommand command) => Play(in command));
        game.Lifecycle.OnActivate<SoundEmitter>(StartEmitter);
        game.Lifecycle.OnDeactivate<SoundEmitter>(StopEmitter);
    }

    /// <summary>The mixer a device renders; tests render it through a <see cref="Devices.NullAudioDevice"/>.</summary>
    public Mixer Mixer { get; } = new();

    public AudioSettings Settings { get; } = new();

    /// <summary>The ambience's gain while the listener stands in an indoor volume.</summary>
    public float IndoorAmbienceGain { get; set; } = 0.6f;

    /// <summary>The ambience's low-pass cutoff in hertz while the listener stands in an indoor volume.</summary>
    public float IndoorAmbienceCutoff { get; set; } = 1200;

    /// <summary>Whether the listener stood in an indoor volume at the last update.</summary>
    public bool IsIndoors => _indoors;

    /// <summary>Where the ears were at the last update.</summary>
    public Vector3 Listener => _listener;

    public SoundHandle Play(string sound) => Start(sound, null, Entity.Null);

    public SoundHandle Play(string sound, Vector3 position) => Start(sound, position, Entity.Null);

    public SoundHandle Play(string sound, Entity entity) => Start(sound, null, entity);

    public void Stop(SoundHandle handle, float fadeSeconds = 0.05f)
    {
        if (!_playing.Remove(handle.Id))
        {
            return;
        }

        float frames = fadeSeconds * AudioFormat.SampleRate;
        MixerCommand stop = MixerCommand.Stop(handle.Id, (long)frames);
        Mixer.Send(in stop);
    }

    public bool IsPlaying(SoundHandle handle)
    {
        TakeFinished();
        return _playing.ContainsKey(handle.Id);
    }

    public override void Update(in GameTime t)
    {
        _now += t.Elapsed;
        TakeFinished();
        FindListener();
        foreach ((int id, Playing playing) in _playing)
        {
            if (!playing.Spatial)
            {
                continue;
            }

            (float left, float right) = Place(playing);
            if (MathF.Abs(left - playing.Left) > GainEpsilon || MathF.Abs(right - playing.Right) > GainEpsilon)
            {
                playing.Left = left;
                playing.Right = right;
                MixerCommand set = MixerCommand.SetVoice(id, left, right);
                Mixer.Send(in set);
            }
        }

        bool indoors = IsListenerIndoors();
        if (indoors != _indoors)
        {
            _indoors = indoors;
            _busesChanged = true;
        }

        if (_busesChanged)
        {
            _busesChanged = false;
            SendBuses();
        }

        if (_game.IsPaused != _paused)
        {
            _paused = _game.IsPaused;
            foreach (AudioBus bus in WorldBuses)
            {
                MixerCommand pause = MixerCommand.PauseBus(bus, _paused);
                Mixer.Send(in pause);
            }
        }
    }

    public override void Dispose()
    {
        Settings.Changed -= HandleSettingsChanged;
        _library?.Dispose();
        base.Dispose();
    }

    private void Play(in PlaySoundCommand command)
    {
        if (command.Entity != Entity.Null && World.IsAlive(command.Entity))
        {
            Play(command.Sound, command.Entity);
        }
        else if (command.Position is { } position)
        {
            Play(command.Sound, position);
        }
        else
        {
            Play(command.Sound);
        }
    }

    private SoundHandle Start(string sound, Vector3? position, Entity entity)
    {
        SoundDefinition? definition = FindDefinition(sound);
        if (definition is null || definition.Files.Count == 0)
        {
            return SoundHandle.None;
        }

        if (!_states.TryGetValue(sound, out SoundState? state))
        {
            state = new SoundState();
            _states[sound] = state;
        }

        TimeSpan since = _now - state.LastPlay;
        if (definition.Throttle > 0 && state.Played && since.TotalSeconds < definition.Throttle)
        {
            return SoundHandle.None;
        }

        int variation = PickVariation(definition.Files.Count, state.LastVariation);
        SoundClip? clip = LoadClip(definition.Files[variation]);
        if (clip is null)
        {
            return SoundHandle.None;
        }

        state.Played = true;
        state.LastPlay = _now;
        state.LastVariation = variation;
        float quieter = _random.NextSingle() * definition.GainVariation;
        float detune = ((_random.NextSingle() * 2) - 1) * definition.PitchVariation;
        float pitch = definition.Pitch * (1 + detune);
        ClipSource source = new(clip, definition.Loop, pitch);
        bool placed = position is not null || entity != Entity.Null;
        Playing playing = new()
        {
            Definition = definition,
            Gain = definition.Gain * (1 - quieter),
            Position = position ?? Vector3.Zero,
            Entity = entity,
            Spatial = definition.Spatial && placed,
        };
        FindListener();
        (playing.Left, playing.Right) = Place(playing);
        _lastId++;
        MixerCommand play = MixerCommand.Play(_lastId, source, definition.Bus, playing.Left, playing.Right);
        Mixer.Send(in play);
        _playing[_lastId] = playing;
        return new SoundHandle(_lastId);
    }

    /// <summary>The gains a sound has now: its own to both ears when it has no place, else panned and faded from the listener, following its entity.</summary>
    private (float Left, float Right) Place(Playing playing)
    {
        if (!playing.Spatial)
        {
            return (playing.Gain, playing.Gain);
        }

        if (playing.Entity != Entity.Null && World.IsAlive(playing.Entity) && World.TryGet(playing.Entity, out Transform transform))
        {
            playing.Position = transform.Position;
        }

        SoundDefinition definition = playing.Definition;
        (float left, float right) = Panning.Compute(_listener, _right, playing.Position, definition.MinDistance, definition.MaxDistance);
        return (left * playing.Gain, right * playing.Gain);
    }

    private int PickVariation(int count, int last)
    {
        if (count == 1)
        {
            return 0;
        }

        int pick = _random.Next(count - (last >= 0 ? 1 : 0));
        if (last >= 0 && pick >= last)
        {
            pick++;
        }

        return pick;
    }

    private SoundDefinition? FindDefinition(string sound)
    {
        DataAssets? data = _game.Services.GetService<DataAssets>();
        if (string.IsNullOrEmpty(sound))
        {
            return null;
        }

        if (data is null)
        {
            WarnOnce(sound, "the game has no data assets service to read it from.");
            return null;
        }

        try
        {
            return data.Load<SoundDefinition>(sound);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            WarnOnce(sound, exception.Message);
            return null;
        }
    }

    /// <summary>The next voice id, for whoever else plays through the mixer.</summary>
    internal int TakeVoiceId()
    {
        _lastId++;
        return _lastId;
    }

    /// <summary>The clip of a sound file in the content folder; null, with a warning, when it can't be read.</summary>
    internal SoundClip? LoadClip(string file)
    {
        if (_library is null)
        {
            IContentManager? content = _game.Services.GetService<IContentManager>();
            if (content is null)
            {
                WarnOnce(file, "the game has no content manager service to read it from.");
                return null;
            }

            _library = new SoundLibrary(content);
        }

        try
        {
            return _library.Load(file);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            WarnOnce(file, exception.Message);
            return null;
        }
    }

    private void WarnOnce(string path, string problem)
    {
        if (_warned.Add(path))
        {
            LogSoundProblem(path, problem);
        }
    }

    private void TakeFinished()
    {
        while (Mixer.TryTakeFinished(out int voice))
        {
            _playing.Remove(voice);
        }
    }

    /// <summary>Puts the ears at the listener entity (the camera without one) and turns them with the camera.</summary>
    private void FindListener()
    {
        RenderCamera? camera = ActiveCamera.Find(World);
        Vector3? ears = null;
        World.Query(in Listeners, (ref AudioListener listener, ref Transform transform) =>
        {
            ears ??= transform.Position + new Vector3(0, listener.Height, 0);
        });

        if (camera is { } view)
        {
            _right = Vector3.Transform(Vector3.UnitX, view.Transform.Rotation);
            ears ??= view.Transform.Position;
        }

        _listener = ears ?? _listener;
    }

    private bool IsListenerIndoors()
    {
        bool inside = false;
        Vector3 ears = _listener;
        World.Query(in Indoors, (ref IndoorVolume volume, ref Transform transform) =>
        {
            Quaternion toVolume = Quaternion.Conjugate(transform.Rotation);
            Vector3 local = Vector3.Transform(ears - transform.Position, toVolume);
            Vector3 half = volume.Size / 2;
            inside |= MathF.Abs(local.X) <= half.X && MathF.Abs(local.Z) <= half.Z && local.Y >= 0 && local.Y <= volume.Size.Y;
        });
        return inside;
    }

    private void SendBuses()
    {
        foreach (AudioBus bus in Enum.GetValues<AudioBus>())
        {
            float gain = Settings.GetVolume(bus);
            float cutoff = MixerCommand.NoCutoff;
            if (bus == AudioBus.Ambience && _indoors)
            {
                gain *= IndoorAmbienceGain;
                cutoff = IndoorAmbienceCutoff;
            }

            MixerCommand set = MixerCommand.SetBus(bus, gain, cutoff);
            Mixer.Send(in set);
        }
    }

    private void HandleSettingsChanged()
    {
        _busesChanged = true;
    }

    private void StartEmitter(Entity entity, ref SoundEmitter emitter)
    {
        SoundHandle handle = Play(emitter.Sound, entity);
        if (!handle.IsNone)
        {
            _emitters[entity] = handle;
        }
    }

    private void StopEmitter(Entity entity, ref SoundEmitter emitter)
    {
        if (_emitters.Remove(entity, out SoundHandle handle))
        {
            Stop(handle, EmitterFadeSeconds);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The sound {Path} can't be played: {Problem}")]
    private partial void LogSoundProblem(string path, string problem);

    private sealed class Playing
    {
        public required SoundDefinition Definition { get; init; }

        public float Gain { get; init; }

        public Vector3 Position { get; set; }

        public Entity Entity { get; init; }

        public bool Spatial { get; init; }

        public float Left { get; set; }

        public float Right { get; set; }
    }

    private sealed class SoundState
    {
        public bool Played { get; set; }

        public TimeSpan LastPlay { get; set; }

        public int LastVariation { get; set; } = -1;
    }
}
