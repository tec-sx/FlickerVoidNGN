using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>
/// Plays <c>music</c> data assets as layers (a frame system and the <see cref="IMusicPlayer"/> service). Each bus has a stack of
/// cues, the top one heard; a cue's grid is counted in the mixer's frames from the frame its first bar began on, so every track
/// starts and stops on an exact frame of it. Each frame the system finds which tracks of the cue heard are wanted (asked for by a
/// <see cref="MusicArea"/> someone stands in or by a script, and passing their conditions) and brings the others in or out on
/// the grid, with their fills; throws the scatter tracks' sounds around the listener; and sends <see cref="MusicBeatEvent"/>s.
/// A cue pushed over another holds it at the bar it reached, and it continues from there. Saves keep the cues and their bars.
/// </summary>
public sealed partial class MusicSystem : BaseSystem<World, GameTime>, IMusicPlayer
{
    /// <summary>How far ahead of the mixer anything is scheduled, so the command is there before its frame.</summary>
    public const int LookaheadFrames = 1024;

    /// <summary>The frames a stopped loop fades over at least, so it ends without a click.</summary>
    public const int DeclickFrames = 240;

    private static readonly QueryDescription Areas = new QueryDescription().WithAll<MusicArea, TriggerArea>();
    private static readonly QueryDescription Activators = new QueryDescription().WithAll<TriggerActivator>();
    private static readonly QueryDescription Listeners = new QueryDescription().WithAll<AudioListener>();
    private static readonly QueryDescription Scenes = new QueryDescription().WithAll<SceneMusic>();

    private readonly Game _game;
    private readonly AudioSystem _audio;
    private readonly ILogger _logger;
    private readonly Dictionary<(SoundClip, double), SoundClip> _stretched = [];
    private readonly Random _random;
    private readonly Dictionary<AudioBus, List<CueState>> _stacks = [];
    private readonly Dictionary<string, int> _requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _asked = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Entity> _activators = [];
    private readonly HashSet<string> _muted = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _soloed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _scales = new(StringComparer.OrdinalIgnoreCase);
    private double _now;
    private bool _sceneLeft;

    public MusicSystem(Game game, AudioSystem audio, Random? random = null)
        : base(game.World)
    {
        _game = game;
        _audio = audio;
        _logger = game.LoggerFactory.CreateLogger<MusicSystem>();
        _random = random ?? new Random();
        game.Lifecycle.OnActivate<SceneMusic>(EnterScene);
        game.Lifecycle.OnDeactivate<SceneMusic>(LeaveScene);
        game.SaveStates.AddGlobal<MusicSaveState>("music", Save, Restore);
    }

    /// <summary>Whether the tracks' <c>when</c> conditions are taken as true, as an editor auditioning a cue wants them.</summary>
    public bool IgnoreConditions { get; set; }

    private Mixer Mixer => _audio.Mixer;

    public void Play(string cue)
    {
        MusicCue? definition = FindCue(cue);
        if (definition is null)
        {
            return;
        }

        List<CueState> stack = GetStackState(definition.Bus);
        if (stack.Count > 0 && IsCue(stack[0], cue))
        {
            return;
        }

        CueState state = CreateState(cue, definition);
        if (stack.Count == 0)
        {
            state.Start = Mixer.Position + LookaheadFrames;
            state.ActiveFrom = state.Start;
            stack.Add(state);
        }
        else if (stack.Count == 1)
        {
            long at = NextBar(stack[0]);
            Hold(stack[0], at);
            state.Start = at;
            state.ActiveFrom = at;
            stack[0] = state;
        }
        else
        {
            state.Held = true;
            stack[0] = state;
        }
    }

    public void Stop(AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count == 0)
        {
            return;
        }

        CueState top = stack[^1];
        long at = NextBar(top);
        Hold(top, at);
        stack.Clear();
    }

    public void Push(string cue)
    {
        MusicCue? definition = FindCue(cue);
        if (definition is null)
        {
            return;
        }

        List<CueState> stack = GetStackState(definition.Bus);
        if (definition.TempoFromBelow && stack.Count > 0)
        {
            definition = definition.WithGridOf(stack[^1].Cue);
        }

        CueState state = CreateState(cue, definition);
        long start = Mixer.Position + LookaheadFrames;
        if (stack.Count > 0)
        {
            CueState top = stack[^1];
            start = NextBar(top);
            if (state.FillIn is { } fill)
            {
                PlayOnce(fill, definition.Bus, definition.Gain, start);
                start += GridLength(definition, fill);
            }

            Hold(top, start);
            top.Held = true;
            top.HeldFrames = Math.Max(0, start - top.Start);
        }

        state.Start = start;
        state.ActiveFrom = start;
        stack.Add(state);
    }

    public void Pop(AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count < 2)
        {
            Stop(bus);
            return;
        }

        CueState top = stack[^1];
        CueState below = stack[^2];
        long resume = NextBar(top);
        Hold(top, resume);
        if (top.FillOut is { } fill)
        {
            PlayOnce(fill, bus, top.Cue.Gain, resume);
            resume += GridLength(top.Cue, fill);
        }

        below.HeldFrames = GetContinueFrames(below);
        below.Start = resume - below.HeldFrames;
        below.ActiveFrom = resume;
        below.Held = false;
        below.Fresh = true;
        stack.RemoveAt(stack.Count - 1);
    }

    public void RequestLayer(string layer)
    {
        _requests.TryGetValue(layer, out int count);
        _requests[layer] = count + 1;
    }

    public void ReleaseLayer(string layer)
    {
        if (!_requests.TryGetValue(layer, out int count))
        {
            return;
        }

        if (count <= 1)
        {
            _requests.Remove(layer);
        }
        else
        {
            _requests[layer] = count - 1;
        }
    }

    public void PlayStinger(string file, MusicQuantize quantize = MusicQuantize.Beat, float gain = 1)
    {
        SoundClip? clip = _audio.LoadClip(file);
        if (clip is null)
        {
            return;
        }

        long at = Mixer.Position + LookaheadFrames;
        List<CueState> stack = GetStackState(AudioBus.Music);
        if (stack.Count > 0)
        {
            CueState top = stack[^1];
            long earliest = Math.Max(at, top.ActiveFrom);
            at = NextLine(top, earliest, quantize);
        }

        PlayOnce(clip, AudioBus.Music, gain, at);
    }

    public string? GetCurrent(AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        return stack.Count == 0 ? null : stack[^1].Name;
    }

    /// <summary>The cues of a bus's stack, the one heard first.</summary>
    public IReadOnlyList<MusicStackEntry> GetStack(AudioBus bus)
    {
        List<CueState> stack = GetStackState(bus);
        List<MusicStackEntry> entries = [];
        for (int index = stack.Count - 1; index >= 0; index--)
        {
            CueState state = stack[index];
            double barFrames = BarFrames(state.Cue);
            double played = state.Held ? state.HeldFrames : Mixer.Position - state.Start;
            double bars = barFrames > 0 ? Math.Max(0, played) / barFrames : 0;
            int layers = state.Held ? state.HeldLayers : state.Tracks.Count(track => track.On);
            entries.Add(new MusicStackEntry(state.Name, state.Held, state.Held ? bars : 0, bars, layers));
        }

        return entries;
    }

    public MusicPosition GetPosition(AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count == 0)
        {
            return new MusicPosition(1, 1, 0);
        }

        CueState top = stack[^1];
        double beatFrames = BeatFrames(top.Cue);
        long played = Mixer.Position - top.Start;
        if (beatFrames <= 0 || played < 0)
        {
            return new MusicPosition(1, 1, 0);
        }

        double beats = played / beatFrames;
        long beat = (long)beats;
        int perBar = top.Cue.BeatsPerBar;
        long bar = beat / perBar;
        long inBar = beat % perBar;
        return new MusicPosition((int)bar + 1, (int)inBar + 1, beats / perBar);
    }

    public bool IsLayerOn(string layer, AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count == 0)
        {
            return false;
        }

        foreach (TrackState track in stack[^1].Tracks)
        {
            if (track.On && string.Equals(track.Track.Name, layer, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The mixer frame a bar of the cue heard on a bus begins on, the bars counted from 1 with fractions; -1 without a cue or a tempo.</summary>
    public long GetFrameOfBar(double bar, AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count == 0)
        {
            return -1;
        }

        CueState top = stack[^1];
        double barFrames = BarFrames(top.Cue);
        if (barFrames <= 0)
        {
            return -1;
        }

        double frames = (bar - 1) * barFrames;
        return top.Start + (long)Math.Round(frames);
    }

    /// <summary>
    /// Plays a cue held in memory, as an editor holds it while it is edited, in place of its bus's cues and from a bar, at once and
    /// without fills; what is changed on the cue's values is heard as it plays.
    /// </summary>
    public void Preview(string name, MusicCue cue, double bar = 1)
    {
        StopNow(cue.Bus);
        CueState state = CreateState(name, cue);
        long at = Mixer.Position + LookaheadFrames;
        double behind = Math.Max(0, bar - 1) * BarFrames(cue);
        state.Start = at - (long)Math.Round(behind);
        state.ActiveFrom = at;
        GetStackState(cue.Bus).Add(state);
    }

    public void Seek(double bar, AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count == 0)
        {
            return;
        }

        CueState top = stack[^1];
        double barFrames = BarFrames(top.Cue);
        if (barFrames <= 0)
        {
            return;
        }

        long at = Mixer.Position + LookaheadFrames;
        Hold(top, at);
        double behind = Math.Max(0, bar - 1) * barFrames;
        top.Start = at - (long)Math.Round(behind);
        top.ActiveFrom = at;
        top.Fresh = true;
        top.Held = false;
        top.LastBeat = -1;
        top.LastBar = double.NaN;
        top.ShotsUntil = 0;
    }

    public void StopNow(AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        long now = Mixer.Position;
        foreach (CueState cue in stack)
        {
            Hold(cue, now);
            foreach (TrackState track in cue.Tracks)
            {
                if (track.FillVoice != 0)
                {
                    MixerCommand stop = MixerCommand.Stop(track.FillVoice, DeclickFrames);
                    Mixer.Send(in stop);
                    track.FillVoice = 0;
                }
            }
        }

        stack.Clear();
    }

    public void SetTrackMuted(string layer, bool muted)
    {
        if (muted)
        {
            _muted.Add(layer);
        }
        else
        {
            _muted.Remove(layer);
        }
    }

    public void SetTrackSoloed(string layer, bool soloed)
    {
        if (soloed)
        {
            _soloed.Add(layer);
        }
        else
        {
            _soloed.Remove(layer);
        }
    }

    public void SetTrackGain(string layer, float gain)
    {
        _scales[layer] = Math.Max(0, gain);
    }

    public float TakeLayerLevel(string layer, AudioBus bus = AudioBus.Music)
    {
        List<CueState> stack = GetStackState(bus);
        if (stack.Count == 0)
        {
            return 0;
        }

        foreach (TrackState track in stack[^1].Tracks)
        {
            if (string.Equals(track.Track.Name, layer, StringComparison.OrdinalIgnoreCase))
            {
                float loop = track.LoopVoice == 0 ? 0 : Mixer.TakeVoicePeak(track.LoopVoice);
                float fill = track.FillVoice == 0 ? 0 : Mixer.TakeVoicePeak(track.FillVoice);
                return MathF.Max(loop, fill);
            }
        }

        return 0;
    }

    public override void Update(in GameTime t)
    {
        if (!_game.IsPaused)
        {
            _now += t.Elapsed.TotalSeconds;
        }

        if (_sceneLeft)
        {
            _sceneLeft = false;
            StopWithoutScene();
        }

        FindAsked();
        ConditionContext context = MakeContext();
        foreach ((AudioBus bus, List<CueState> stack) in _stacks)
        {
            if (stack.Count == 0)
            {
                continue;
            }

            CueState top = stack[^1];
            foreach (TrackState track in top.Tracks)
            {
                bool wanted = IsWanted(track, context);
                if (wanted && !track.On)
                {
                    TurnOn(top, track);
                }
                else if (!wanted && track.On)
                {
                    TurnOff(top, track);
                }

                if (track.On && track.LoopVoice != 0)
                {
                    ApplyGain(top, track);
                }

                if (track.On && track.Track.Scatter.Length > 0 && !_game.IsPaused && _now >= track.NextScatter)
                {
                    Scatter(track);
                }
            }

            top.Fresh = false;
            if (bus == AudioBus.Music)
            {
                SendBeat(top);
            }

            SendNotifies(top);
            PlayShots(top);
        }
    }

    private static AudioBus BusOf(CueState cue, MusicTrack track) => track.Bus ?? cue.Cue.Bus;

    /// <summary>Starts the sounds of the notifies whose bars come within the next frames, each on its frame of the grid.</summary>
    private void PlayShots(CueState cue)
    {
        double barFrames = BarFrames(cue.Cue);
        long soonest = Math.Max(Mixer.Position + LookaheadFrames, cue.ActiveFrom);
        long from = Math.Max(cue.ShotsUntil, soonest);
        long until = Mixer.Position + (LookaheadFrames * 3);
        if (barFrames <= 0 || until <= from)
        {
            return;
        }

        cue.ShotsUntil = until;
        int phrase = cue.Cue.PhraseBars;
        foreach (MusicNotify notify in cue.Cue.Notifies)
        {
            if (notify.Sound.Length == 0)
            {
                continue;
            }

            double offset = (notify.Bar - 1) * barFrames;
            if (!notify.Repeat || phrase <= 0)
            {
                PlayShot(cue, notify, cue.Start + (long)Math.Round(offset), from, until);
                continue;
            }

            double phraseFrames = phrase * barFrames;
            long first = (long)Math.Floor((from - cue.Start - offset) / phraseFrames);
            long final = (long)Math.Floor((until - cue.Start - offset) / phraseFrames);
            for (long round = Math.Max(0, first); round <= final; round++)
            {
                PlayShot(cue, notify, cue.Start + (long)Math.Round(offset + (round * phraseFrames)), from, until);
            }
        }
    }

    private void PlayShot(CueState cue, MusicNotify notify, long at, long from, long until)
    {
        if (at < from || at >= until)
        {
            return;
        }

        float gain = cue.Cue.Gain * notify.Gain;
        AudioBus bus = cue.Cue.Bus;
        if (notify.Track.Length > 0)
        {
            TrackState? track = cue.Tracks.FirstOrDefault(track => string.Equals(track.Track.Name, notify.Track, StringComparison.OrdinalIgnoreCase));
            if (track is null || !track.On)
            {
                return;
            }

            gain = TrackGain(cue, track) * notify.Gain;
            bus = BusOf(cue, track.Track);
        }

        SoundClip? clip = _audio.LoadClip(notify.Sound);
        if (clip is not null)
        {
            PlayOnce(clip, bus, gain, at);
        }
    }

    private static bool IsCue(CueState state, string cue) => string.Equals(state.Name, cue, StringComparison.OrdinalIgnoreCase);

    private static double BeatFrames(MusicCue cue) => cue.Tempo > 0 ? AudioFormat.SampleRate * 60.0 / cue.Tempo : 0;

    private static long GetContinueFrames(CueState cue)
    {
        if (cue.Cue.ContinuePoints.Count == 0)
        {
            return cue.HeldFrames;
        }

        double barFrames = BarFrames(cue.Cue);
        List<long> points = cue.Cue.ContinuePoints.Select(point => (long)Math.Round(Math.Max(0, point.Bar - 1) * barFrames)).Order().ToList();
        foreach (long point in points)
        {
            if (point >= cue.HeldFrames)
            {
                return point;
            }
        }

        return points[0];
    }

    private static double BarFrames(MusicCue cue) => BeatFrames(cue) * cue.BeatsPerBar;

    private static long ClipFrames(SoundClip clip)
    {
        double frames = clip.Frames * (double)AudioFormat.SampleRate / clip.SampleRate;
        return (long)Math.Round(frames);
    }

    /// <summary>The frames a clip played once takes on a cue's grid: its length rounded to whole bars, at least one; its own length without a grid.</summary>
    private static long GridLength(MusicCue cue, SoundClip clip)
    {
        long frames = ClipFrames(clip);
        double barFrames = BarFrames(cue);
        if (barFrames <= 0)
        {
            return frames;
        }

        double bars = Math.Round(frames / barFrames);
        bars = Math.Max(1, bars);
        return (long)Math.Round(bars * barFrames);
    }

    /// <summary>The first line of the cue's grid on or after a frame; the frame itself without a grid.</summary>
    private static long NextLine(CueState cue, long from, MusicQuantize quantize)
    {
        double barFrames = BarFrames(cue.Cue);
        double unit = MusicGrid.GetUnit(cue.Cue, quantize) * barFrames;
        if (unit <= 0)
        {
            return from;
        }

        double line = MusicGrid.NextLine(from - cue.Start, unit);
        return cue.Start + (long)Math.Round(line);
    }

    private long NextBar(CueState cue)
    {
        long earliest = Math.Max(Mixer.Position + LookaheadFrames, cue.ActiveFrom);
        return NextLine(cue, earliest, MusicQuantize.Bar);
    }

    private List<CueState> GetStackState(AudioBus bus)
    {
        if (!_stacks.TryGetValue(bus, out List<CueState>? stack))
        {
            stack = [];
            _stacks[bus] = stack;
        }

        return stack;
    }

    private CueState CreateState(string name, MusicCue cue)
    {
        CueState state = new() { Name = name, Cue = cue };
        state.FillIn = LoadOptional(cue.FillIn);
        state.FillOut = LoadOptional(cue.FillOut);
        foreach (MusicTrack track in cue.Tracks)
        {
            TrackState trackState = new()
            {
                Track = track,
                Loop = LoadStretched(track.Loop, cue, track),
                FillIn = LoadStretched(track.FillIn, cue, track),
                FillOut = LoadStretched(track.FillOut, cue, track),
            };
            state.Tracks.Add(trackState);
        }

        return state;
    }

    private SoundClip? LoadStretched(string file, MusicCue cue, MusicTrack track)
    {
        SoundClip? clip = LoadOptional(file);
        if (clip is null || track.Tempo <= 0 || cue.Tempo <= 0 || Math.Abs(track.Tempo - cue.Tempo) < 0.01f)
        {
            return clip;
        }

        double ratio = track.Tempo / cue.Tempo;
        if (!_stretched.TryGetValue((clip, ratio), out SoundClip? stretched))
        {
            stretched = TimeStretch.Stretch(clip, ratio);
            _stretched[(clip, ratio)] = stretched;
        }

        return stretched;
    }

    private SoundClip? LoadOptional(string file) => file.Length == 0 ? null : _audio.LoadClip(file);

    private MusicCue? FindCue(string cue)
    {
        if (string.IsNullOrEmpty(cue))
        {
            return null;
        }

        DataAssets? data = _game.Services.GetService<DataAssets>();
        if (data is null)
        {
            WarnOnce(cue, "the game has no data assets service to read it from.");
            return null;
        }

        try
        {
            return data.Load<MusicCue>(cue);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            WarnOnce(cue, exception.Message);
            return null;
        }
    }

    private void WarnOnce(string path, string problem)
    {
        if (_warned.Add(path))
        {
            LogCueProblem(path, problem);
        }
    }

    /// <summary>Gathers the track names asked for now: by scripts, and by the music areas an activator stands in.</summary>
    private void FindAsked()
    {
        _asked.Clear();
        foreach (string layer in _requests.Keys)
        {
            _asked.Add(layer);
        }

        TriggerAreaSystem? triggers = _game.Services.GetService<TriggerAreaSystem>();
        if (triggers is null)
        {
            return;
        }

        _activators.Clear();
        World.Query(in Activators, (Entity entity) => _activators.Add(entity));
        World.Query(in Areas, (Entity entity, ref MusicArea area) =>
        {
            foreach (Entity activator in _activators)
            {
                if (triggers.IsInside(entity, activator))
                {
                    _asked.UnionWith(area.Layers);
                    return;
                }
            }
        });
    }

    /// <summary>A context for the tracks' conditions, with the listener as who acts.</summary>
    private ConditionContext MakeContext()
    {
        Entity listener = Entity.Null;
        World.Query(in Listeners, (Entity entity) =>
        {
            if (listener == Entity.Null)
            {
                listener = entity;
            }
        });
        WorldEntityView? view = listener == Entity.Null ? null : new WorldEntityView(World, listener);
        return new ConditionContext(_game.Facts) { Instigator = view, Services = _game.Services };
    }

    private bool IsAsked(MusicTrack track)
    {
        if (!track.IsRequested)
        {
            return true;
        }

        foreach (string asked in _asked)
        {
            if (track.IsAskedBy(asked))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWanted(TrackState track, ConditionContext context)
    {
        if (!IsAsked(track.Track))
        {
            return false;
        }

        return IgnoreConditions || track.Track.When.IsEmpty || track.Track.When.Evaluate(context);
    }

    /// <summary>Brings a track in: at once with a cue that is starting or continuing, else on its line of the grid after its fill-in.</summary>
    private void TurnOn(CueState cue, TrackState track)
    {
        track.On = true;
        track.NextScatter = _now + NextWait(track.Track);
        if (track.Loop is not { } loop)
        {
            return;
        }

        MusicTrack settings = track.Track;
        float gain = TrackGain(cue, track);
        track.AppliedGain = gain;
        long start = cue.ActiveFrom;
        if (!cue.Fresh)
        {
            long soonest = Math.Max(Mixer.Position + LookaheadFrames, cue.ActiveFrom);
            long earliest = Math.Max(soonest, track.BusyUntil);
            start = NextLine(cue, earliest, settings.Start);
            if (track.FillIn is { } fill)
            {
                track.FillVoice = PlayOnce(fill, BusOf(cue, settings), gain, start);
                track.FillStart = start;
                start += GridLength(cue.Cue, fill);
            }
        }

        double barFrames = BarFrames(cue.Cue);
        double period = ClipFrames(loop);
        double offset = 0;
        if (barFrames > 0)
        {
            double fileBars = Math.Round(period / barFrames);
            double bars = settings.Bars > 0 ? settings.Bars : Math.Max(1, fileBars);
            period = bars * barFrames;
            if (settings.Phase == MusicPhase.Locked)
            {
                offset = (start - cue.Start) % period;
            }
        }

        GridLoopSource source = new(loop, period, offset);
        float fadeIn = settings.FadeIn * AudioFormat.SampleRate;
        track.LoopVoice = _audio.TakeVoiceId();
        track.LoopStart = start;
        AudioBus bus = BusOf(cue, settings);
        MixerCommand play = MixerCommand.PlayOnGrid(track.LoopVoice, source, bus, gain, start, (long)fadeIn);
        Mixer.Send(in play);
    }

    /// <summary>Takes a track out on its line of the grid with its fill-out; one still in its fill-in just never starts its loop.</summary>
    private void TurnOff(CueState cue, TrackState track)
    {
        track.On = false;
        if (track.LoopVoice == 0)
        {
            return;
        }

        long position = Mixer.Position;
        if (track.LoopStart > position + LookaheadFrames)
        {
            MixerCommand cancel = MixerCommand.Stop(track.LoopVoice, 0);
            Mixer.Send(in cancel);
            track.BusyUntil = track.LoopStart;
            track.LoopVoice = 0;
            return;
        }

        long earliest = Math.Max(position + LookaheadFrames, track.LoopStart);
        long at = NextLine(cue, earliest, track.Track.Stop);
        StopLoop(track, at);
        if (track.FillOut is { } fill)
        {
            float gain = TrackGain(cue, track);
            AudioBus bus = BusOf(cue, track.Track);
            PlayOnce(fill, bus, gain, at);
        }

        track.BusyUntil = at;
    }

    /// <summary>Stops everything of a cue from a frame on: loops playing fade from it, and what would start no sooner never sounds.</summary>
    private void Hold(CueState cue, long at)
    {
        cue.HeldLayers = cue.Tracks.Count(track => track.On);
        foreach (TrackState track in cue.Tracks)
        {
            if (track.LoopVoice != 0)
            {
                StopLoop(track, at);
            }

            if (track.FillVoice != 0 && track.FillStart >= at)
            {
                MixerCommand cancel = MixerCommand.Stop(track.FillVoice, 0);
                Mixer.Send(in cancel);
            }

            track.FillVoice = 0;
            track.On = false;
            track.BusyUntil = 0;
        }
    }

    private void StopLoop(TrackState track, long at)
    {
        float fadeOut = track.Track.FadeOut * AudioFormat.SampleRate;
        long fade = Math.Max(DeclickFrames, (long)fadeOut);
        MixerCommand stop = MixerCommand.StopAt(track.LoopVoice, at, fade);
        Mixer.Send(in stop);
        track.LoopVoice = 0;
    }

    private float TrackGain(CueState cue, TrackState track)
    {
        string name = track.Track.Name;
        if (_muted.Contains(name) || (_soloed.Count > 0 && !_soloed.Contains(name)))
        {
            return 0;
        }

        float scale = _scales.TryGetValue(name, out float own) ? own : 1;
        return cue.Cue.Gain * track.Track.Gain * scale;
    }

    private void ApplyGain(CueState cue, TrackState track)
    {
        float gain = TrackGain(cue, track);
        if (MathF.Abs(gain - track.AppliedGain) < 0.0001f)
        {
            return;
        }

        track.AppliedGain = gain;
        MixerCommand set = MixerCommand.SetVoice(track.LoopVoice, gain, gain);
        Mixer.Send(in set);
    }

    private int PlayOnce(SoundClip clip, AudioBus bus, float gain, long at)
    {
        ClipSource source = new(clip);
        int voice = _audio.TakeVoiceId();
        MixerCommand play = MixerCommand.PlayOnGrid(voice, source, bus, gain, at);
        Mixer.Send(in play);
        return voice;
    }

    private double NextWait(MusicTrack track)
    {
        float longest = Math.Max(track.EveryMin, track.EveryMax);
        return track.EveryMin + (_random.NextDouble() * (longest - track.EveryMin));
    }

    private void Scatter(TrackState track)
    {
        MusicTrack settings = track.Track;
        track.NextScatter = _now + NextWait(settings);
        double angle = _random.NextDouble() * Math.Tau;
        float farthest = Math.Max(settings.NearDistance, settings.FarDistance);
        float distance = settings.NearDistance + (_random.NextSingle() * (farthest - settings.NearDistance));
        (double sin, double cos) = Math.SinCos(angle);
        Vector3 away = new((float)cos * distance, 0, (float)sin * distance);
        _audio.Play(settings.Scatter, _audio.Listener + away);
    }

    private void SendBeat(CueState cue)
    {
        double beatFrames = BeatFrames(cue.Cue);
        long played = Mixer.Position - cue.Start;
        if (beatFrames <= 0 || played < 0)
        {
            return;
        }

        long beat = (long)(played / beatFrames);
        if (beat == cue.LastBeat)
        {
            return;
        }

        cue.LastBeat = beat;
        int perBar = cue.Cue.BeatsPerBar;
        long bar = beat / perBar;
        long inBar = beat % perBar;
        _game.Events.Send(new MusicBeatEvent(cue.Name, (int)bar + 1, (int)inBar + 1));
    }

    /// <summary>Sends the notifies of the cue the music passed since the last frame; one whose bar repeats with the phrase sends once for each phrase, and one of a track only while that track sounds.</summary>
    private void SendNotifies(CueState cue)
    {
        double barFrames = BarFrames(cue.Cue);
        long played = Mixer.Position - cue.Start;
        if (barFrames <= 0 || played < 0)
        {
            return;
        }

        double bar = played / barFrames;
        double last = cue.LastBar;
        cue.LastBar = bar;
        if (double.IsNaN(last))
        {
            return;
        }

        SendSections(cue, last, bar);
        if (cue.Cue.Notifies.Count == 0)
        {
            return;
        }

        int phrase = cue.Cue.PhraseBars;
        foreach (MusicNotify notify in cue.Cue.Notifies)
        {
            double offset = notify.Bar - 1;
            if (!notify.Repeat || phrase <= 0)
            {
                SendNotify(cue, notify, offset, last, bar);
                continue;
            }

            long first = (long)Math.Floor(last / phrase);
            long final = (long)Math.Floor(bar / phrase);
            for (long round = first; round <= final; round++)
            {
                SendNotify(cue, notify, (round * phrase) + offset, last, bar);
            }
        }
    }

    private void SendNotify(CueState cue, MusicNotify notify, double at, double last, double bar)
    {
        if (at <= last || at > bar)
        {
            return;
        }

        if (notify.Track.Length > 0 && !cue.Tracks.Any(track => track.On && string.Equals(track.Track.Name, notify.Track, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        int whole = (int)Math.Floor(at) + 1;
        _game.Events.Send(new MusicNotifyEvent(cue.Name, notify.Name, whole, notify.Track));
    }

    private void SendSections(CueState cue, double last, double bar)
    {
        double begins = 0;
        foreach (MusicSection section in cue.Cue.Sections)
        {
            if (begins > last && begins <= bar)
            {
                _game.Events.Send(new MusicSectionEvent(cue.Name, section.Name, (int)Math.Floor(begins) + 1));
            }

            begins += section.Bars;
        }
    }

    private void EnterScene(Entity entity, ref SceneMusic scene)
    {
        Play(scene.Music);
        Play(scene.Ambience);
    }

    private void LeaveScene(Entity entity, ref SceneMusic scene)
    {
        _sceneLeft = true;
    }

    /// <summary>Stops the music and the ambience when the scene that played them is gone and no other took its place.</summary>
    private void StopWithoutScene()
    {
        int scenes = World.CountEntities(in Scenes);
        if (scenes == 0)
        {
            Stop(AudioBus.Music);
            Stop(AudioBus.Ambience);
        }
    }

    private MusicSaveState? Save()
    {
        MusicSaveState state = new();
        state.Layers.AddRange(_requests.Keys);
        long position = Mixer.Position;
        foreach ((AudioBus bus, List<CueState> stack) in _stacks)
        {
            foreach (CueState cue in stack)
            {
                long played = cue.Held ? cue.HeldFrames : position - cue.Start;
                double barFrames = BarFrames(cue.Cue);
                double bars = barFrames > 0 && played > 0 ? Math.Floor(played / barFrames) : 0;
                state.Cues.Add(new SavedCue { Cue = cue.Name, Bus = bus, Bars = (int)bars });
            }
        }

        return state.Cues.Count == 0 && state.Layers.Count == 0 ? null : state;
    }

    /// <summary>Puts a save's cues back, each bus's lowest held and its top continuing from the bar it had reached.</summary>
    private void Restore(MusicSaveState state)
    {
        long now = Mixer.Position;
        foreach (List<CueState> stack in _stacks.Values)
        {
            foreach (CueState cue in stack)
            {
                Hold(cue, now);
            }

            stack.Clear();
        }

        _requests.Clear();
        foreach (string layer in state.Layers)
        {
            _requests[layer] = 1;
        }

        long start = now + LookaheadFrames;
        foreach (SavedCue saved in state.Cues)
        {
            MusicCue? definition = FindCue(saved.Cue);
            if (definition is null)
            {
                continue;
            }

            List<CueState> stack = GetStackState(saved.Bus);
            if (stack.Count > 0)
            {
                stack[^1].Held = true;
            }

            CueState cue = CreateState(saved.Cue, definition);
            double played = saved.Bars * BarFrames(definition);
            cue.HeldFrames = (long)Math.Round(played);
            cue.Start = start - cue.HeldFrames;
            cue.ActiveFrom = start;
            stack.Add(cue);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The music {Path} can't be played: {Problem}")]
    private partial void LogCueProblem(string path, string problem);

    private sealed class CueState
    {
        public required string Name { get; init; }

        public required MusicCue Cue { get; init; }

        public List<TrackState> Tracks { get; } = [];

        public SoundClip? FillIn { get; set; }

        public SoundClip? FillOut { get; set; }

        /// <summary>The mixer frame its first bar began on, or would have.</summary>
        public long Start { get; set; }

        /// <summary>The mixer frame it is heard from: its start, or where it continues after being held.</summary>
        public long ActiveFrom { get; set; }

        /// <summary>Whether its tracks have yet to be started, at once and without fill-ins.</summary>
        public bool Fresh { get; set; } = true;

        public bool Held { get; set; }

        /// <summary>How many of its tracks sounded when it was last held.</summary>
        public int HeldLayers { get; set; }

        /// <summary>The frames it had played when it was held.</summary>
        public long HeldFrames { get; set; }

        public long LastBeat { get; set; } = -1;

        /// <summary>The bar (from 0, with fractions) the notifies were last sent up to; NaN before the first frame.</summary>
        public double LastBar { get; set; } = double.NaN;

        /// <summary>The mixer frame the notifies' sounds were last started up to.</summary>
        public long ShotsUntil { get; set; }
    }

    private sealed class TrackState
    {
        public required MusicTrack Track { get; init; }

        public SoundClip? Loop { get; init; }

        public SoundClip? FillIn { get; init; }

        public SoundClip? FillOut { get; init; }

        public bool On { get; set; }

        /// <summary>The gain last sent to its loop, with the mixer's mute, solo and level on it.</summary>
        public float AppliedGain { get; set; }

        public int LoopVoice { get; set; }

        public long LoopStart { get; set; }

        public int FillVoice { get; set; }

        public long FillStart { get; set; }

        /// <summary>The frame its last loop stops on, before which no new one comes in.</summary>
        public long BusyUntil { get; set; }

        /// <summary>When its next scattered sound is due, in seconds of play.</summary>
        public double NextScatter { get; set; }
    }
}
