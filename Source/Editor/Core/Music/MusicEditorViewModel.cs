using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Timelines;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>
/// A music cue as a DAW: the transport (play, stop, the position, tempo, meter, phrase and snap), the ruler with its playhead,
/// the rehearsal rows that stand for the game's music areas, a lane per track with its clips, notifiers, the mixer and mix states,
/// the selected clip, and the lists of sounds and cues to drag in. <paramref name="edited"/> follows each change to the cue, and
/// the lanes are laid out again after it; what plays follows the cue as it is edited.
/// </summary>
public sealed partial class MusicEditorViewModel : ObservableObject, IDisposable, ITransport
{
    public const double DefaultTotalBars = 24;

    private const double MeterFall = 1.6;
    private const double MinBarWidth = 10;
    private const double MaxBarWidth = 160;

    private readonly MusicCue _cue;
    private readonly string _path;
    private readonly Action _edited;
    private readonly ClipCatalog _catalog;
    private readonly JsonTypeInfo _trackType;
    private readonly FindContentFiles? _files;
    private readonly MusicEditorServices _services;
    private MusicPreview? _preview;
    private double _startedAt;

    [ObservableProperty]
    private MusicTrackViewModel? _selected;

    [ObservableProperty]
    private ClipSelection? _selectedClip;

    [ObservableProperty]
    private double _totalBars = DefaultTotalBars;

    [ObservableProperty]
    private double _barWidth = MusicLayout.DefaultBarWidth;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _loopPlayback;

    /// <summary>The cue of the timeline whose settings the Cue tab shows.</summary>
    [ObservableProperty]
    private NotifierViewModel? _selectedCue;

    [ObservableProperty]
    private bool _hasLoopRange;

    /// <summary>The bars a loop range spans, from 0.</summary>
    [ObservableProperty]
    private double _loopStart;

    [ObservableProperty]
    private double _loopEnd;

    /// <summary>Where the playhead is, in bars from the cue's start (0 is the start of bar 1).</summary>
    [ObservableProperty]
    private double _playhead;

    [ObservableProperty]
    private MusicQuantize _snap = MusicQuantize.Bar;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>The tab shown in the side panel: 0 Track, 1 Cue.</summary>
    [ObservableProperty]
    private int _sideTab;

    /// <summary>The list shown in the side panel's lower half: 0 Sounds, 1 Cues.</summary>
    [ObservableProperty]
    private int _listTab;

    /// <summary>The tab shown in the dock under the lanes: 0 Mixer, 1 Clip.</summary>
    [ObservableProperty]
    private int _dockTab;

    [ObservableProperty]
    private bool _dockOpen = true;

    [ObservableProperty]
    private string _search = string.Empty;

    /// <summary>How fast the day passes while an arrangement without a tempo plays.</summary>
    [ObservableProperty]
    private DayPace _dayPace;

    public MusicEditorViewModel(string path, string title, MusicCue cue, JsonTypeInfo cueType, JsonTypeInfo trackType, Action edited, ClipCatalog catalog, MusicEditorServices? services = null, FindContentFiles? files = null)
    {
        _path = path;
        Title = title;
        _cue = cue;
        _services = services ?? new MusicEditorServices();
        _edited = () =>
        {
            edited();
            Refresh();
            if (IsPlaying)
            {
                Restart();
            }
        };
        _catalog = catalog;
        _trackType = trackType;
        _files = files;
        _dayPace = DayPaces[0];
        Cue = new DataObjectViewModel("Arrangement", cue, cueType, _edited, files, ["tracks", "notifies", "sections", "continuePoints", "tempo", "beatsPerBar", "phraseBars"]) { Icon = "Music" };
        Tracks = [];
        foreach (MusicTrack track in cue.Tracks)
        {
            Tracks.Add(MakeTrack(track));
        }

        Notifiers = [];
        foreach (MusicNotify notify in cue.Notifies)
        {
            Notifiers.Add(new NotifierViewModel(notify, _edited));
        }

        Sections = [];
        foreach (MusicSection section in cue.Sections)
        {
            Sections.Add(new SectionViewModel(section, OnSectionsChanged));
        }

        ContinuePoints = [];
        foreach (MusicContinuePoint point in cue.ContinuePoints)
        {
            ContinuePoints.Add(new ContinuePointViewModel(point, _edited));
        }

        StackEntries = [];
        LiveRows = [];
        Rows = [];
        MixStates = [];
        Buses = [];
        foreach (AudioBus bus in Enum.GetValues<AudioBus>().Where(bus => bus != AudioBus.Master).Append(AudioBus.Master))
        {
            Buses.Add(new BusStripViewModel(bus, PushBuses));
        }

        Sounds = [];
        Cues = [];
        Selected = Tracks.FirstOrDefault();
        Refresh();
        RefreshRehearsal();
        RefreshMix();
        RefreshLists();
    }

    public string Title { get; }

    public string Path => _path;

    public ClipCatalog Catalog => _catalog;

    public DataObjectViewModel Cue { get; }

    public ObservableCollection<MusicTrackViewModel> Tracks { get; }

    public ObservableCollection<NotifierViewModel> Notifiers { get; }

    public ObservableCollection<SectionViewModel> Sections { get; }

    public ObservableCollection<ContinuePointViewModel> ContinuePoints { get; }

    /// <summary>The cues the preview keeps on the stack, the one heard first.</summary>
    public ObservableCollection<StackEntryViewModel> StackEntries { get; }

    /// <summary>The Live dock's rows: each track with whether it sounds and what it plays on or waits for.</summary>
    public ObservableCollection<LiveRowViewModel> LiveRows { get; }

    private void RefreshLive()
    {
        bool same = LiveRows.Count == Tracks.Count && LiveRows.Select(row => row.Name).SequenceEqual(Tracks.Select(track => track.Track.Name));
        if (!same)
        {
            LiveRows.Clear();
            foreach (MusicTrackViewModel track in Tracks)
            {
                LiveRows.Add(new LiveRowViewModel(track.Track.Name));
            }
        }

        for (int index = 0; index < Tracks.Count; index++)
        {
            MusicTrackViewModel track = Tracks[index];
            bool on = IsPlaying && IsLayerOn(track);
            LiveRows[index].IsOn = on;
            LiveRows[index].State = on ? "sounding" : DescribeWait(track);
        }
    }

    private string DescribeWait(MusicTrackViewModel track)
    {
        if (track.Track.IsRequested)
        {
            List<string> areas = Rows.Where(row => row.IsArea && row.Layers.Any(track.Track.IsAskedBy)).Select(row => row.Label.Replace("Rehearsal · ", string.Empty)).ToList();
            return areas.Count > 0 ? $"in {string.Join(", ", areas)}" : "asked by script";
        }

        if (!track.Track.When.IsEmpty)
        {
            IReadOnlyList<RehearsalSpan> hours = MusicArrangement.GetHours(track.Track.When);
            if (hours.Count > 0 && !(hours.Count == 1 && hours[0].On == 0 && hours[0].Off >= MusicArrangement.DayHours))
            {
                return $"waits for {(int)hours[0].On:00}:00";
            }
        }

        return IsPlaying ? "waits" : "plays always";
    }

    /// <summary>The sections laid one after the other from the start of the cue.</summary>
    public IReadOnlyList<SectionBand> SectionBands
    {
        get
        {
            List<SectionBand> bands = [];
            double start = 0;
            for (int index = 0; index < _cue.Sections.Count; index++)
            {
                MusicSection section = _cue.Sections[index];
                bands.Add(new SectionBand(section.Name, start, section.Bars, index));
                start += section.Bars;
            }

            return bands;
        }
    }

    /// <summary>The rehearsal's rows: the music areas of the open scene that ask for this cue's tracks, and scripts that do.</summary>
    public ObservableCollection<RehearsalRow> Rows { get; }

    public ObservableCollection<MixStateViewModel> MixStates { get; }

    public ObservableCollection<BusStripViewModel> Buses { get; }

    public ObservableCollection<SoundItem> Sounds { get; }

    public ObservableCollection<SoundItem> Cues { get; }

    public IReadOnlyList<MusicQuantize> SnapOptions { get; } = Enum.GetValues<MusicQuantize>();

    /// <summary>The paces the day can pass at while an arrangement without a tempo plays; the last holds the hour.</summary>
    public IReadOnlyList<int> BufferOptions => MusicPreview.BufferOptions;

    /// <summary>The frames the sound device takes at a time while the editor plays.</summary>
    public int BufferFrames
    {
        get => _services.BufferFrames();
        set
        {
            if (value <= 0 || value == _services.BufferFrames())
            {
                return;
            }

            _services.SetBufferFrames?.Invoke(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(BufferText));
        }
    }

    /// <summary>How late the buffer makes the sound, in milliseconds.</summary>
    public string BufferText
    {
        get
        {
            double milliseconds = Math.Round(BufferFrames * 1000.0 / AudioFormat.SampleRate);
            return $"{milliseconds:0} ms";
        }
    }

    public IReadOnlyList<DayPace> DayPaces { get; } = [new DayPace("10 s an hour", 0.1), new DayPace("2 s an hour", 0.5), new DayPace("Held", 0)];

    /// <summary>What the ruler counts, beside the arrangement's name over the lanes' headers.</summary>
    public string RulerText => IsHours ? "arrangement · hours of the day" : "arrangement · bars";

    public double BarSeconds => MusicArrangement.GetBarSeconds(_cue);

    public bool HasTracks => Tracks.Count > 0;

    public bool HasRows => Rows.Count > 0;

    /// <summary>Why nothing is heard, from the preview; empty when all is well.</summary>
    public string Problem => _preview?.Problem ?? string.Empty;

    public decimal Tempo
    {
        get => (decimal)_cue.Tempo;
        set
        {
            _cue.Tempo = (float)Math.Clamp(value, 0, 400);
            OnPropertyChanged();
            _edited();
        }
    }

    public decimal BeatsPerBar
    {
        get => _cue.BeatsPerBar;
        set
        {
            _cue.BeatsPerBar = (int)Math.Clamp(value, 1, 16);
            OnPropertyChanged();
            _edited();
        }
    }

    public decimal PhraseBars
    {
        get => _cue.PhraseBars;
        set
        {
            _cue.PhraseBars = (int)Math.Clamp(value, 1, 64);
            OnPropertyChanged();
            _edited();
        }
    }

    public string BarText => (Math.Floor(Playhead) + 1).ToString("0");

    /// <summary>The playhead as bar, beat and sixteenth, counted from 1: <c>12.2.1</c>.</summary>
    public string PositionText
    {
        get
        {
            if (IsHours)
            {
                return HourText;
            }

            double bar = Math.Floor(Playhead);
            double beats = (Playhead - bar) * _cue.BeatsPerBar;
            double beat = Math.Floor(beats);
            double sixteenth = Math.Floor((beats - beat) * 4);
            return $"{bar + 1:0}.{beat + 1:0}.{sixteenth + 1:0}";
        }
    }

    /// <summary>Whether the cue has no tempo, so its lanes show the hours of the day: the playhead is the time of day it is heard at.</summary>
    public bool IsHours => _cue.Tempo <= 0;

    private string HourText
    {
        get
        {
            int minutes = (int)Math.Round(Playhead * 60);
            return $"{minutes / 60 % 24:00}:{minutes % 60:00}";
        }
    }

    public string TimeText
    {
        get
        {
            if (IsHours)
            {
                return HourText;
            }

            double seconds = Playhead * BarSeconds;
            int minutes = (int)(seconds / 60);
            double rest = seconds - (minutes * 60);
            return $"{minutes}:{rest:00.0}";
        }
    }

    public int LayersSounding => Tracks.Count(track => track.Level > 0.0005 || (IsPlaying && !track.Muted && IsLayerOn(track)));

    public string LayersText => $"{Tracks.Count(track => IsPlaying && IsLayerOn(track))} of {Tracks.Count} layers sounding";

    public bool HasMixStates => MixStates.Count > 0;

    public string StateHint => "Each layer goes to the bus under its fader. Layer faders belong to the arrangement, bus faders to the project. A mix state is a set of bus values the game blends to: turn one on to hear it.";

    partial void OnPlayheadChanged(double value)
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(BarText));
    }

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(LayersText));

    /// <summary>The transport's loop: <see cref="LoopPlayback"/>.</summary>
    public bool IsLooping
    {
        get => LoopPlayback;
        set => LoopPlayback = value;
    }

    partial void OnLoopPlaybackChanged(bool value) => OnPropertyChanged(nameof(IsLooping));

    void ITransport.TogglePlay() => Play();

    /// <summary>Where the selected layer is, under its name in the panel's header: the arrangement and the layer's place in it.</summary>
    public string SelectedPlace
    {
        get
        {
            if (Selected is not { } selected)
            {
                return string.Empty;
            }

            int place = Tracks.IndexOf(selected) + 1;
            return $"{Title} / layer {place}";
        }
    }

    partial void OnSelectedChanged(MusicTrackViewModel? oldValue, MusicTrackViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectedPlace));
        if (SelectedClip is { } clip && clip.Track != newValue)
        {
            SelectedClip = null;
        }
    }

    partial void OnSearchChanged(string value) => RefreshLists();

    private bool IsLayerOn(MusicTrackViewModel track) => _preview?.Music.IsLayerOn(track.Track.Name, _cue.Bus) == true;

    private MusicTrackViewModel MakeTrack(MusicTrack track)
    {
        MusicTrackViewModel model = new(track, _trackType, _edited, _files, () => _cue.Tempo, () => _cue.Bus);
        model.MixChanged += PushTrackMix;
        return model;
    }

    [RelayCommand]
    private void AddTrack()
    {
        MusicTrack track = new() { Name = $"Layer {Tracks.Count + 1}" };
        _cue.Tracks.Add(track);
        MusicTrackViewModel model = MakeTrack(track);
        Tracks.Add(model);
        Selected = model;
        _edited();
        RefreshRehearsal();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        int index = Tracks.IndexOf(selected);
        _cue.Tracks.Remove(selected.Track);
        Tracks.Remove(selected);
        Selected = Tracks.Count > 0 ? Tracks[Math.Min(index, Tracks.Count - 1)] : null;
        _edited();
        RefreshRehearsal();
    }

    /// <summary>Removes what is picked: the cue, else the clip, whose file is taken off its layer, else the layer.</summary>
    [RelayCommand]
    private void DeleteSelection()
    {
        if (SelectedCue is { } cue)
        {
            RemoveNotifier(cue);
            return;
        }

        if (SelectedClip is { } clip)
        {
            ClearClip(clip);
            return;
        }

        RemoveSelected();
    }

    private void ClearClip(ClipSelection clip)
    {
        MusicTrack track = clip.Track.Track;
        switch (clip.Clip.Kind)
        {
            case ArrangementKind.FillIn:
                track.FillIn = string.Empty;
                break;
            case ArrangementKind.FillOut:
                track.FillOut = string.Empty;
                break;
            default:
                track.Loop = string.Empty;
                break;
        }

        SelectedClip = null;
        _edited();
    }

    /// <summary>Picks a layer by its lane or its header; the cue picked before is let go.</summary>
    public void SelectLayer(MusicTrackViewModel layer)
    {
        SelectedCue = null;
        SelectedClip = null;
        Selected = layer;
    }

    [RelayCommand]
    private void MoveUp() => Move(-1);

    [RelayCommand]
    private void MoveDown() => Move(1);

    private void Move(int by)
    {
        if (Selected is not { } selected)
        {
            return;
        }

        int index = Tracks.IndexOf(selected);
        int target = index + by;
        if (target < 0 || target >= Tracks.Count)
        {
            return;
        }

        Tracks.Move(index, target);
        _cue.Tracks.RemoveAt(index);
        _cue.Tracks.Insert(target, selected.Track);
        OnPropertyChanged(nameof(SelectedPlace));
        _edited();
    }

    [RelayCommand]
    private void AddNotifier() => AddNotifierAt(Math.Floor(Playhead));

    /// <summary>Places a cue once on the timeline at a bar counted from 0 with fractions, snapped to a quarter bar, and selects it.</summary>
    public void AddNotifierAt(double bar)
    {
        double snapped = Math.Round(Math.Clamp(bar, 0, TotalBars - 0.25) * 4) / 4;
        MusicNotify notify = new() { Name = $"Cue {Notifiers.Count + 1}", Bar = (float)(snapped + 1) };
        _cue.Notifies.Add(notify);
        NotifierViewModel added = new(notify, _edited);
        Notifiers.Add(added);
        SelectedCue = added;
        SideTab = 1;
        _edited();
    }

    public void RemoveNotifier(NotifierViewModel notifier)
    {
        _cue.Notifies.Remove(notifier.Notify);
        Notifiers.Remove(notifier);
        if (SelectedCue == notifier)
        {
            SelectedCue = null;
        }

        _edited();
    }

    [RelayCommand]
    private void RemoveSelectedCue()
    {
        if (SelectedCue is { } cue)
        {
            RemoveNotifier(cue);
        }
    }

    public void MoveNotifier(NotifierViewModel notifier, double bar)
    {
        double snapped = Math.Round(Math.Clamp(bar, 0, TotalBars - 0.25) * 4) / 4;
        notifier.Notify.Bar = (float)(snapped + 1);
        notifier.Refresh();
        _edited();
    }

    /// <summary>Selects a cue of the timeline and shows its settings in the Cue tab.</summary>
    public void SelectCue(NotifierViewModel? cue)
    {
        SelectedCue = cue;
        if (cue is not null)
        {
            SideTab = 1;
        }
    }

    /// <summary>Picks the bars between two points of the timeline to play in a loop; the transport's loop then keeps to them.</summary>
    public void SetLoopRange(double from, double to)
    {
        double start = Math.Clamp(Math.Min(from, to), 0, TotalBars);
        double end = Math.Clamp(Math.Max(from, to), 0, TotalBars);
        if (end - start < 0.25)
        {
            ClearLoopRange();
            return;
        }

        LoopStart = start;
        LoopEnd = end;
        HasLoopRange = true;
        LoopPlayback = true;
    }

    [RelayCommand]
    private void ClearLoopRange()
    {
        HasLoopRange = false;
        LoopStart = 0;
        LoopEnd = 0;
    }

    [RelayCommand]
    private void AddSection()
    {
        MusicSection section = new() { Name = $"Section {Sections.Count + 1}", Bars = 4 };
        _cue.Sections.Add(section);
        Sections.Add(new SectionViewModel(section, OnSectionsChanged));
        SideTab = 1;
        OnSectionsChanged();
    }

    public void RemoveSection(SectionViewModel section)
    {
        _cue.Sections.Remove(section.Section);
        Sections.Remove(section);
        OnSectionsChanged();
    }

    private void OnSectionsChanged()
    {
        OnPropertyChanged(nameof(SectionBands));
        _edited();
    }

    [RelayCommand]
    private void AddContinuePoint()
    {
        MusicContinuePoint point = new() { Name = $"Point {ContinuePoints.Count + 1}", Bar = (float)Math.Floor(Playhead) + 1 };
        _cue.ContinuePoints.Add(point);
        ContinuePoints.Add(new ContinuePointViewModel(point, _edited));
        SideTab = 1;
        _edited();
    }

    public void RemoveContinuePoint(ContinuePointViewModel point)
    {
        _cue.ContinuePoints.Remove(point.Point);
        ContinuePoints.Remove(point);
        _edited();
    }

    /// <summary>Pushes a cue of Content over the one playing, as the game does for the hub's song; plays the cue first when nothing plays.</summary>
    [RelayCommand]
    private void PushCue(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!IsPlaying)
        {
            Play();
        }

        _preview?.Music.Push(path);
    }

    /// <summary>Takes the cue on top off the stack; the one below continues from its continue point.</summary>
    [RelayCommand]
    private void PopCue() => _preview?.Music.Pop(_cue.Bus);

    private void FollowStack()
    {
        if (_preview is null)
        {
            return;
        }

        IReadOnlyList<MusicStackEntry> stack = _preview.Music.GetStack(_cue.Bus);
        bool same = stack.Count == StackEntries.Count && stack.Select(entry => entry.Cue).SequenceEqual(StackEntries.Select(entry => entry.Name));
        if (!same)
        {
            StackEntries.Clear();
            foreach (MusicStackEntry entry in stack)
            {
                StackEntries.Add(new StackEntryViewModel(entry.Cue));
            }
        }

        for (int index = 0; index < stack.Count; index++)
        {
            MusicStackEntry entry = stack[index];
            StackEntryViewModel row = StackEntries[index];
            row.IsTop = index == 0;
            row.State = entry.IsHeld
                ? $"held at bar {entry.HeldBars + 1:0.#} · {entry.Layers} layers"
                : $"on top · bar {entry.Bars + 1:0.#}";
        }
    }

    public void Refresh()
    {
        _catalog.Forget();
        Layout();
    }

    private void Layout()
    {
        List<RehearsalSpan> spans = Rows.Select(row => row.Span).ToList();
        for (int index = 0; index < Tracks.Count; index++)
        {
            MusicTrackViewModel track = Tracks[index];
            track.Index = index;
            track.Refresh(_cue, _catalog, SpansFor(track, spans), TotalBars);
        }

        OnPropertyChanged(nameof(BarSeconds));
        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(Tempo));
        OnPropertyChanged(nameof(IsHours));
        OnPropertyChanged(nameof(RulerText));
        OnPropertyChanged(nameof(BeatsPerBar));
        OnPropertyChanged(nameof(PhraseBars));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(LayersText));
        RefreshLive();
    }

    private IReadOnlyList<RehearsalSpan> SpansFor(MusicTrackViewModel track, IReadOnlyList<RehearsalSpan> all)
    {
        List<RehearsalSpan> spans = [];
        foreach (RehearsalRow row in Rows)
        {
            if (row.Layers.Any(track.Track.IsAskedBy))
            {
                spans.Add(row.Span);
            }
        }

        return spans;
    }

    /// <summary>Builds the rehearsal's rows from the open scene's music areas and from scripts, for every track that is played on request.</summary>
    public void RefreshRehearsal()
    {
        Dictionary<string, RehearsalSpan> kept = Rows.ToDictionary(row => row.Label, row => row.Span);
        Rows.Clear();
        List<MusicTrack> wanted = Tracks.Where(track => track.ByRequest).Select(track => track.Track).ToList();
        HashSet<MusicTrack> covered = [];
        int index = 0;
        foreach (RehearsalSource source in _services.Areas())
        {
            string[] layers = source.Layers.Where(layer => wanted.Any(track => track.IsAskedBy(layer))).ToArray();
            if (layers.Length == 0)
            {
                continue;
            }

            covered.UnionWith(wanted.Where(track => layers.Any(track.IsAskedBy)));
            AddRow(new RehearsalRow(source.Name, layers, true, DefaultSpan(index++)), kept);
        }

        foreach (MusicTrack track in wanted.Where(track => !covered.Contains(track)))
        {
            string asked = track.Triggers.Count > 0 ? track.Triggers[0].Name : track.Name;
            AddRow(new RehearsalRow(asked, [asked], false, DefaultSpan(index++)), kept);
        }

        OnPropertyChanged(nameof(HasRows));
        Layout();
    }

    private void AddRow(RehearsalRow row, Dictionary<string, RehearsalSpan> kept)
    {
        if (kept.TryGetValue(row.Label, out RehearsalSpan span))
        {
            row.Span = span;
        }

        Rows.Add(row);
    }

    private RehearsalSpan DefaultSpan(int index)
    {
        double phrase = Math.Max(1, _cue.PhraseBars);
        double on = Math.Min(TotalBars - 4, phrase + (index * phrase));
        return new RehearsalSpan(on, on + (phrase * 2));
    }

    public void SetSpan(RehearsalRow row, RehearsalSpan span)
    {
        row.Span = span;
        Layout();
    }

    /// <summary>Reads the project's mix states again, keeping those that are on.</summary>
    public void RefreshMix()
    {
        HashSet<string> on = MixStates.Where(state => state.IsOn).Select(state => state.Name).ToHashSet();
        MixStates.Clear();
        MixSet mix = _services.Mix();
        foreach (MixState state in mix.States)
        {
            MixStateViewModel model = new(state, PushMixState) { IsOn = on.Contains(state.Name) };
            MixStates.Add(model);
        }

        OnPropertyChanged(nameof(HasMixStates));
    }

    public void RefreshLists()
    {
        Sounds.Clear();
        Cues.Clear();
        foreach (string path in _services.Sounds())
        {
            if (Matches(path))
            {
                Sounds.Add(new SoundItem(path, Describe));
            }
        }

        foreach (string path in _services.Cues())
        {
            if (Matches(path))
            {
                Cues.Add(new SoundItem(path, DescribeCue));
            }
        }
    }

    private bool Matches(string path) => Search.Length == 0 || path.Contains(Search, StringComparison.OrdinalIgnoreCase);

    /// <summary>A sound file as the lists show it: its bars at the cue's tempo, and the tempo its length says when that is not the cue's.</summary>
    private string Describe(string path)
    {
        if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return "sound";
        }

        ClipInfo? info = _catalog.Find(path);
        if (info is null)
        {
            return "can't be read";
        }

        double found = FoundTempo(info);
        double bars = Math.Max(1, Math.Round(info.Seconds / BarSeconds));
        string text = $"{bars:0} bars";
        if (_cue.Tempo > 0 && Math.Abs(found - _cue.Tempo) > 0.5)
        {
            return $"{text} · {found:0.#} → {_cue.Tempo:0}";
        }

        return $"{text} · {_cue.Tempo:0}";
    }

    private static string DescribeCue(string path)
    {
        return "cue";
    }

    /// <summary>The tempo a file would have if it were a whole number of bars of the cue's meter long.</summary>
    public double FoundTempo(ClipInfo info)
    {
        double barSeconds = BarSeconds;
        double bars = Math.Max(1, Math.Round(info.Seconds / barSeconds));
        return bars * _cue.BeatsPerBar * 60 / info.Seconds;
    }

    /// <summary>Gives a track the sound file as its loop, or fill-in or fill-out when its name says so.</summary>
    public void Assign(string file, MusicTrackViewModel track)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(file);
        if (name.Contains("FillIn", StringComparison.OrdinalIgnoreCase))
        {
            track.Track.FillIn = file;
        }
        else if (name.Contains("FillOut", StringComparison.OrdinalIgnoreCase))
        {
            track.Track.FillOut = file;
        }
        else
        {
            track.Track.Loop = file;
            if (track.Track.Name.StartsWith("Track ", StringComparison.Ordinal))
            {
                track.Track.Name = name;
            }
        }

        Selected = track;
        _edited();
        RefreshRehearsal();
    }

    /// <summary>Drops a sound on the lanes: on a track's lane, it becomes that track's clip; below them, it makes a track.</summary>
    public void DropSound(string file, int row)
    {
        if (row >= 0 && row < Tracks.Count)
        {
            Assign(file, Tracks[row]);
            return;
        }

        MusicTrack track = new() { Name = System.IO.Path.GetFileNameWithoutExtension(file) };
        _cue.Tracks.Add(track);
        MusicTrackViewModel model = MakeTrack(track);
        Tracks.Add(model);
        Assign(file, model);
    }

    /// <summary>Imports sound files from the disk into the project's Content and lists them.</summary>
    public int Import(IReadOnlyList<string> files)
    {
        if (_services.Import is null)
        {
            Status = "This project can't import sound files.";
            return 0;
        }

        IReadOnlyList<string> imported = _services.Import(files);
        RefreshLists();
        Status = imported.Count == 1 ? $"Imported {imported[0]}" : $"Imported {imported.Count} sound files";
        return imported.Count;
    }

    /// <summary>Imports sound files dropped on the lanes and gives the first to the lane they were dropped on.</summary>
    public void ImportAndDrop(IReadOnlyList<string> files, int row)
    {
        if (_services.Import is null)
        {
            Status = "This project can't import sound files.";
            return;
        }

        IReadOnlyList<string> imported = _services.Import(files);
        RefreshLists();
        Status = $"Imported {imported.Count} sound file(s)";
        for (int index = 0; index < imported.Count; index++)
        {
            DropSound(imported[index], index == 0 ? row : Tracks.Count);
        }
    }

    public void OpenFile(string path) => _services.Open?.Invoke(path);

    [RelayCommand]
    private void EditMix() => _services.OpenMix?.Invoke();

    [RelayCommand]
    private void ShowMixer()
    {
        DockTab = 0;
        DockOpen = true;
    }

    [RelayCommand]
    private void ShowLive()
    {
        DockTab = 2;
        DockOpen = true;
    }

    [RelayCommand]
    private void ShowClip()
    {
        DockTab = 1;
        DockOpen = true;
    }

    [RelayCommand]
    private void ZoomIn() => BarWidth = Math.Min(MaxBarWidth, BarWidth * 1.25);

    [RelayCommand]
    private void ZoomOut() => BarWidth = Math.Max(MinBarWidth, BarWidth / 1.25);

    /// <summary>Zooms so that every bar of the lanes fits into a width in pixels.</summary>
    public void FitTo(double width) => BarWidth = Math.Clamp(width / TotalBars, MinBarWidth, MaxBarWidth);

    /// <summary>Selects a clip of a track: the clip dock shows it, and its track is the selected one.</summary>
    public void SelectClip(MusicTrackViewModel track, ArrangementClip clip)
    {
        SelectedCue = null;
        Selected = track;
        SelectedClip = new ClipSelection(track, clip);
        DockTab = 1;
    }

    /// <summary>Sets the track's loop length in bars from dragging its clip's right edge.</summary>
    public void SetLoopBars(MusicTrackViewModel track, int bars)
    {
        track.Track.Bars = Math.Clamp(bars, 1, 256);
        _edited();
    }

    public void SetFade(MusicTrackViewModel track, bool fadeIn, double bars)
    {
        float seconds = (float)Math.Clamp(bars * BarSeconds, 0, 600);
        seconds = MathF.Round(seconds * 20) / 20;
        if (fadeIn)
        {
            track.Track.FadeIn = seconds;
        }
        else
        {
            track.Track.FadeOut = seconds;
        }

        _edited();
    }

    /// <summary>Sets the loop's tempo to the one its length says, so it is stretched to the cue only when it truly differs.</summary>
    [RelayCommand]
    private void FitToCue()
    {
        if (SelectedClip is not { } clip || _catalog.Find(clip.Track.Track.Loop) is not { } info)
        {
            return;
        }

        clip.Track.Track.Tempo = (float)Math.Round(FoundTempo(info), 2);
        _edited();
    }

    [RelayCommand]
    private void ClearStretch()
    {
        if (SelectedClip is { } clip)
        {
            clip.Track.Track.Tempo = 0;
            _edited();
        }
    }

    public ClipInfo? SelectedInfo => SelectedClip is { } clip ? _catalog.Find(clip.Track.Track.Loop) : null;

    public string SelectedFound => SelectedInfo is { } info ? $"{FoundTempo(info):0.0} BPM" : "—";

    public string SelectedLength => SelectedInfo is { } info ? $"{info.Seconds:0.00} s" : "—";

    partial void OnSelectedClipChanged(ClipSelection? value)
    {
        OnPropertyChanged(nameof(SelectedInfo));
        OnPropertyChanged(nameof(SelectedFound));
        OnPropertyChanged(nameof(SelectedLength));
    }

    /// <summary>Opens the preview, once, and plays the cue from the playhead.</summary>
    [RelayCommand]
    private void Play()
    {
        if (IsPlaying)
        {
            Pause();
            return;
        }

        EnsurePreview();
        if (_preview is null)
        {
            Status = "Nothing can be played here.";
            return;
        }

        IsPlaying = true;
        Restart();
    }

    [RelayCommand]
    private void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        _preview?.Music.StopNow(_cue.Bus);
        ReleaseRows();
        IsPlaying = false;
    }

    /// <summary>Stops; stopped already, it goes back to the start.</summary>
    [RelayCommand]
    public void Stop()
    {
        if (IsPlaying)
        {
            Pause();
            return;
        }

        Playhead = 0;
    }

    [RelayCommand]
    private void ToggleLoop() => LoopPlayback = !LoopPlayback;

    private void EnsurePreview()
    {
        if (_preview is null && _services.Preview is { } open)
        {
            _preview = open();
            PushBuses();
            OnPropertyChanged(nameof(Problem));
            foreach (MixStateViewModel state in MixStates)
            {
                PushMixState(state);
            }
        }
    }

    /// <summary>Starts the cue again at the playhead, as after a seek or an edit.</summary>
    private void Restart()
    {
        if (_preview is null)
        {
            return;
        }

        ReleaseRows();
        _preview.Music.IgnoreConditions = !IsHours;
        if (IsHours)
        {
            _preview.SetHour(Playhead);
            _preview.Music.Preview(_path, _cue, 1);
        }
        else
        {
            _preview.Music.Preview(_path, _cue, Playhead + 1);
        }

        _startedAt = Playhead;
        foreach (MusicTrackViewModel track in Tracks)
        {
            PushTrackMix(track);
        }
    }

    /// <summary>Moves the playhead to a bar of the lanes, from 0, snapped; playing, the music jumps there.</summary>
    public void SeekTo(double bars)
    {
        double unit = IsHours ? 0.25 : MusicGrid.GetUnit(_cue, Snap);
        double clamped = Math.Clamp(bars, 0, TotalBars);
        if (unit > 0)
        {
            clamped = Math.Round(clamped / unit) * unit;
        }

        Playhead = clamped;
        if (IsPlaying && _preview is not null)
        {
            ReleaseRows();
            if (IsHours)
            {
                _preview.SetHour(Playhead);
            }
            else
            {
                _preview.Music.Seek(Playhead + 1, _cue.Bus);
            }
        }
    }

    private void ReleaseRows()
    {
        foreach (RehearsalRow row in Rows)
        {
            if (row.Asking)
            {
                row.Asking = false;
                foreach (string layer in row.Layers)
                {
                    _preview?.Music.ReleaseLayer(layer);
                }
            }
        }
    }

    /// <summary>Runs the preview a step and follows it: the playhead, the rows asking for tracks, the meters.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (_preview is null)
        {
            return;
        }

        _preview.Tick(elapsed);
        double fall = MeterFall * elapsed.TotalSeconds;
        if (IsPlaying)
        {
            FollowStack();
            RefreshLive();
            if (IsHours)
            {
                RunDay(elapsed);
            }
            else if (_preview.Music.GetCurrent(_cue.Bus) is not null)
            {
                MusicStackEntry? own = _preview.Music.GetStack(_cue.Bus).Cast<MusicStackEntry?>().FirstOrDefault(entry => entry!.Value.Cue == _path);
                MusicPosition position = _preview.Music.GetPosition(_cue.Bus);
                double bars = own?.Bars ?? position.Bars;
                if (bars >= 0)
                {
                    Playhead = bars;
                }
            }

            double end = HasLoopRange ? LoopEnd : TotalBars;
            if (Playhead >= end)
            {
                if (LoopPlayback)
                {
                    SeekTo(HasLoopRange ? LoopStart : 0);
                }
                else
                {
                    Pause();
                }
            }

            FollowRows();
        }

        foreach (MusicTrackViewModel track in Tracks)
        {
            double raw = _preview.Music.TakeLayerLevel(track.Track.Name, _cue.Bus);
            track.Level = Math.Max(raw, track.Level - fall);
        }

        foreach (BusStripViewModel bus in Buses)
        {
            double raw = _preview.Mixer.TakeBusPeak(bus.Bus);
            bus.Level = Math.Max(raw, bus.Level - fall);
        }

        OnPropertyChanged(nameof(LayersText));
    }

    /// <summary>Lets the day of an arrangement without a tempo pass at the chosen pace, so the hours its layers keep come and go.</summary>
    private void RunDay(TimeSpan elapsed)
    {
        if (DayPace.HoursPerSecond <= 0)
        {
            return;
        }

        Playhead = Math.Min(TotalBars, Playhead + (elapsed.TotalSeconds * DayPace.HoursPerSecond));
        if (Playhead < TotalBars)
        {
            _preview!.SetHour(Playhead);
        }
    }

    private void FollowRows()
    {
        foreach (RehearsalRow row in Rows)
        {
            bool inside = Playhead >= row.Span.On && Playhead < row.Span.Off;
            if (inside == row.Asking)
            {
                continue;
            }

            row.Asking = inside;
            foreach (string layer in row.Layers)
            {
                if (inside)
                {
                    _preview!.Music.RequestLayer(layer);
                }
                else
                {
                    _preview!.Music.ReleaseLayer(layer);
                }
            }
        }
    }

    private void PushTrackMix(MusicTrackViewModel track)
    {
        if (_preview is null)
        {
            return;
        }

        string name = track.Track.Name;
        _preview.Music.SetTrackMuted(name, track.Muted);
        _preview.Music.SetTrackSoloed(name, track.Soloed);
    }

    private void PushMixState(MixStateViewModel state)
    {
        if (_preview is null)
        {
            return;
        }

        _preview.Audio.Mix = new MixSet { States = MixStates.Select(model => model.State).ToList() };
        _preview.Audio.SetMixState(state.Name, state.IsOn);
    }

    private void PushBuses()
    {
        if (_preview is null)
        {
            return;
        }

        bool anySolo = Buses.Any(bus => bus.Soloed);
        foreach (BusStripViewModel bus in Buses)
        {
            bool silent = bus.Muted || (anySolo && !bus.Soloed && bus.Bus != AudioBus.Master);
            float volume = silent ? 0 : (float)bus.Volume;
            _preview.Audio.Settings.Variables[(int)bus.Bus].Value = volume;
        }
    }

    public void Dispose()
    {
        if (IsPlaying)
        {
            Pause();
        }

        _preview = null;
    }
}

/// <summary>The clip selected in the lanes: a piece of a track, which the clip dock shows.</summary>
public sealed record ClipSelection(MusicTrackViewModel Track, ArrangementClip Clip);
