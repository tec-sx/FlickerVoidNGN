using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Spatial;
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
    private static readonly QueryDescription Rooms = new QueryDescription().WithAll<IndoorVolume, RoomReverb, Transform>();
    private static readonly AudioBus[] WorldBuses = [AudioBus.Ambience, AudioBus.Effects, AudioBus.Dialogue];

    private readonly Game _game;
    private readonly ILogger _logger;
    private readonly Random _random;
    private readonly Dictionary<int, Playing> _playing = [];
    private readonly Dictionary<Entity, SoundHandle> _emitters = [];
    private readonly Dictionary<string, SoundState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, SpatialSource> _spatialSources = [];
    private SoundLibrary? _library;
    private SteamAudio? _steam;
    private Vector3 _listener;
    private Vector3 _right = Vector3.UnitX;
    private Vector3 _up = Vector3.UnitY;
    private Vector3 _forward = -Vector3.UnitZ;
    private TimeSpan _now;
    private int _lastId;
    private bool _paused;
    private bool _indoors;
    private bool _busesChanged = true;
    private MixSet? _mix;
    private readonly HashSet<string> _manualStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _mixWeights = new(StringComparer.OrdinalIgnoreCase);
    private RoomReverb? _room;
    private bool _roomSent;

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

    /// <summary>Steam Audio, which gives 3D sounds their head-related direction; null where its library is not there and they are panned.</summary>
    public SteamAudio? SteamAudio
    {
        get => _steam;
        set => _steam = value;
    }

    /// <summary>The mix states the buses follow: the game's <see cref="MixSet.Path"/> when it has one, else the default ones.</summary>
    public MixSet Mix
    {
        get
        {
            _mix ??= LoadMix();
            return _mix;
        }

        set
        {
            _mix = value;
            _busesChanged = true;
        }
    }

    /// <summary>Turns a mix state the game controls (<c>Talking</c>, <c>Hub open</c>) on or off; it fades in or out over its own time.</summary>
    public void SetMixState(string name, bool on)
    {
        bool changed = on ? _manualStates.Add(name) : _manualStates.Remove(name);
        if (changed)
        {
            _busesChanged = true;
        }
    }

    /// <summary>How far in a mix state is, 0 to 1.</summary>
    public float GetMixWeight(string name) => _mixWeights.TryGetValue(name, out float weight) ? weight : 0;

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

            if (_spatialSources.TryGetValue(id, out SpatialSource? spatial))
            {
                PlaceBinaural(playing, spatial);
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

        RoomReverb? room = FindRoom();
        if (!_roomSent || room?.Wet != _room?.Wet || room?.Size != _room?.Size || room?.Damping != _room?.Damping)
        {
            _room = room;
            _roomSent = true;
            RoomReverb heard = room ?? new RoomReverb { Wet = 0 };
            MixerCommand reverb = MixerCommand.SetReverb(heard.Wet, heard.Size, heard.Damping);
            Mixer.Send(in reverb);
        }

        bool indoors = IsListenerIndoors();
        if (indoors != _indoors)
        {
            _indoors = indoors;
            _busesChanged = true;
        }

        FollowMixStates((float)t.Elapsed.TotalSeconds);
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
        ClipSource clipSource = new(clip, definition.Loop, pitch);
        ISoundSource source = clipSource;
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
        _lastId++;
        SpatialSource? spatial = null;
        if (playing.Spatial && _steam is not null)
        {
            spatial = new SpatialSource(clipSource, _steam);
            source = spatial;
            _spatialSources[_lastId] = spatial;
            PlaceBinaural(playing, spatial);
            playing.Left = 1;
            playing.Right = 1;
        }
        else
        {
            (playing.Left, playing.Right) = Place(playing);
        }

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

    /// <summary>Points a sound's binaural effect at where it is now, and fades it by its distance as the plain panning does.</summary>
    private void PlaceBinaural(Playing playing, SpatialSource spatial)
    {
        if (playing.Entity != Entity.Null && World.IsAlive(playing.Entity) && World.TryGet(playing.Entity, out Transform transform))
        {
            playing.Position = transform.Position;
        }

        SoundDefinition definition = playing.Definition;
        Vector3 toSource = playing.Position - _listener;
        spatial.Direction = Spatial.SteamAudio.ToListenerSpace(toSource, _right, _up, _forward);
        spatial.Gain = Panning.GetAttenuation(toSource.Length(), definition.MinDistance, definition.MaxDistance) * playing.Gain;
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
            if (_spatialSources.Remove(voice, out SpatialSource? spatial))
            {
                spatial.Dispose();
            }
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
            _up = Vector3.Transform(Vector3.UnitY, view.Transform.Rotation);
            _forward = Vector3.Transform(-Vector3.UnitZ, view.Transform.Rotation);
            ears ??= view.Transform.Position;
        }

        _listener = ears ?? _listener;
    }

    /// <summary>The reverberation of the first room with one that the listener stands in; null outdoors.</summary>
    private RoomReverb? FindRoom()
    {
        RoomReverb? found = null;
        Vector3 ears = _listener;
        World.Query(in Rooms, (ref IndoorVolume volume, ref RoomReverb room, ref Transform transform) =>
        {
            if (found is not null)
            {
                return;
            }

            Quaternion toVolume = Quaternion.Conjugate(transform.Rotation);
            Vector3 local = Vector3.Transform(ears - transform.Position, toVolume);
            Vector3 half = volume.Size / 2;
            if (MathF.Abs(local.X) <= half.X && MathF.Abs(local.Z) <= half.Z && local.Y >= 0 && local.Y <= volume.Size.Y)
            {
                found = room;
            }
        });
        return found;
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

    private MixSet LoadMix()
    {
        DataAssets? data = _game.Services.GetService<DataAssets>();
        IContentManager? content = _game.Services.GetService<IContentManager>();
        if (data is null || content is null)
        {
            return MixSet.Default();
        }

        string full = content.GetFullPath(MixSet.Path);
        if (!File.Exists(full))
        {
            return MixSet.Default();
        }

        try
        {
            return data.Load<MixSet>(MixSet.Path);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            _logger.LogWarning("The mix {Path} can't be read: {Problem}", MixSet.Path, exception.Message);
            return MixSet.Default();
        }
    }

    /// <summary>Moves each mix state's weight toward 1 while it holds and 0 while it does not, over its fade.</summary>
    private void FollowMixStates(float seconds)
    {
        foreach (MixState state in Mix.States)
        {
            bool holds = _manualStates.Contains(state.Name)
                || (_indoors && string.Equals(state.Name, MixSet.Indoors, StringComparison.OrdinalIgnoreCase))
                || (_paused && string.Equals(state.Name, MixSet.Paused, StringComparison.OrdinalIgnoreCase));
            float target = holds ? 1 : 0;
            float weight = GetMixWeight(state.Name);
            if (weight == target)
            {
                continue;
            }

            float step = state.Fade <= 0 ? 1 : seconds / state.Fade;
            weight = target > weight ? Math.Min(target, weight + step) : Math.Max(target, weight - step);
            _mixWeights[state.Name] = weight;
            _busesChanged = true;
        }
    }

    private void SendBuses()
    {
        foreach (AudioBus bus in Enum.GetValues<AudioBus>())
        {
            float gain = Settings.GetVolume(bus);
            float cutoff = MixerCommand.NoCutoff;
            foreach (MixState state in Mix.States)
            {
                float weight = GetMixWeight(state.Name);
                if (weight <= 0)
                {
                    continue;
                }

                foreach (MixDuck duck in state.Ducks)
                {
                    if (duck.Bus != bus)
                    {
                        continue;
                    }

                    gain *= 1 + ((duck.Gain - 1) * weight);
                    float dulled = MixerCommand.NoCutoff + ((duck.Cutoff - MixerCommand.NoCutoff) * weight);
                    cutoff = Math.Min(cutoff, dulled);
                }
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
