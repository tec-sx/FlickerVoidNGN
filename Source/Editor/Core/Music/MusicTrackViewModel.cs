using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>One layer of an arrangement as a lane: its clips on the grid, and its values in cards for the side panel.</summary>
public sealed partial class MusicTrackViewModel : ObservableObject
{
    private static readonly string[] AllNames = ["name", "icon", "loop", "tempo", "bars", "fillIn", "fillOut", "gain", "bus", "triggers", "byRequest", "when", "start", "stop", "phase", "fadeIn", "fadeOut", "scatter", "everyMin", "everyMax", "nearDistance", "farDistance"];

    /// <summary>The cards of the Layer tab, each with its title, its icon and the values it holds; the gain, the bus and the icon are set elsewhere.</summary>
    private static readonly (string Title, string Icon, string[] Names)[] Cards =
    [
        ("Layer", "Layers", ["name"]),
        ("Plays when asked for", "Tag", ["triggers", "byRequest", "when"]),
        ("Clips", "Clip", ["loop", "fillIn", "fillOut", "tempo", "bars"]),
        ("Timing", "Timer", ["start", "stop", "phase", "fadeIn", "fadeOut"]),
        ("Scatter", "Scatter", ["scatter", "everyMin", "everyMax"]),
        ("Where", "Where", ["nearDistance", "farDistance"]),
    ];

    /// <summary>The buses a layer can be mixed on; the master carries them all.</summary>
    private static readonly AudioBus[] Buses = [AudioBus.Music, AudioBus.Ambience, AudioBus.Effects, AudioBus.Dialogue, AudioBus.Interface];

    private const string LoopIcon = "Clip";

    private const string ScatterIcon = "Scatter";

    private readonly Action _edited;
    private readonly Func<float> _cueTempo;
    private readonly Func<AudioBus> _cueBus;

    [ObservableProperty]
    private bool _muted;

    [ObservableProperty]
    private bool _soloed;

    /// <summary>The loudest sample the track put out lately, 0 to 1, falling away between ticks; drawn as its meter.</summary>
    [ObservableProperty]
    private double _level;

    [ObservableProperty]
    private IReadOnlyList<ArrangementClip> _clips = [];

    [ObservableProperty]
    private TrackLengths _lengths;

    /// <summary>Its place among the arrangement's layers, from 0, which gives it its colour.</summary>
    [ObservableProperty]
    private int _index;

    /// <summary>Whether it is the layer the side panel shows.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Whether the layer is heard now: playing, on, not muted and not soloed out. Its strip is faint otherwise.</summary>
    [ObservableProperty]
    private bool _isSounding;

    public MusicTrackViewModel(MusicTrack track, JsonTypeInfo typeInfo, Action edited, FindContentFiles? files, Func<float>? cueTempo = null, Func<AudioBus>? cueBus = null)
    {
        Track = track;
        _edited = edited;
        _cueTempo = cueTempo ?? (() => 0);
        _cueBus = cueBus ?? (() => AudioBus.Music);
        List<DataObjectViewModel> forms = [];
        foreach ((string title, string icon, string[] names) in Cards)
        {
            string[] hidden = AllNames.Where(name => !names.Contains(name)).ToArray();
            forms.Add(new DataObjectViewModel(title, track, typeInfo, edited, files, hidden) { Icon = icon });
        }

        Forms = forms;
    }

    public MusicTrack Track { get; }

    public IReadOnlyList<DataObjectViewModel> Forms { get; }

    public string Title => Track.Name.Length > 0 ? Track.Name : "Layer";

    /// <summary>The icons a layer can be shown with, the one of its kind first.</summary>
    public IReadOnlyList<string> IconChoices { get; } = [LoopIcon, "Waveform", "Drum", "Guitar", "Piano", "Trigger", "Dog", "Wind", "Music", "Bell", ScatterIcon];

    /// <summary>The name of the icon the lanes and the panel show the layer with: its own, or the one of its kind.</summary>
    public string Icon
    {
        get
        {
            if (Track.Icon.Length > 0)
            {
                return Track.Icon;
            }

            return IsScatter ? ScatterIcon : LoopIcon;
        }

        set
        {
            string kind = IsScatter ? ScatterIcon : LoopIcon;
            string own = value == kind ? string.Empty : value;
            if (own == Track.Icon)
            {
                return;
            }

            Track.Icon = own;
            OnPropertyChanged();
            _edited();
        }
    }

    /// <summary>The buses the layer can go to, by name.</summary>
    public IReadOnlyList<string> BusChoices { get; } = Buses.Select(bus => bus.ToString()).ToArray();

    /// <summary>The bus the layer goes to: its own, or the arrangement's.</summary>
    public AudioBus Bus => Track.Bus ?? _cueBus();

    /// <summary>The bus the layer goes to. The arrangement's own is not written to the layer, so it follows the arrangement.</summary>
    public string BusChoice
    {
        get => Bus.ToString();
        set
        {
            if (!Enum.TryParse(value, out AudioBus named) || !Buses.Contains(named))
            {
                return;
            }

            AudioBus? bus = named == _cueBus() ? null : named;
            if (bus == Track.Bus)
            {
                return;
            }

            Track.Bus = bus;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BusText));
            _edited();
        }
    }

    /// <summary>Where the layer is mixed, as its panel's chip says it.</summary>
    public string BusText => $"to {BusChoice} bus";

    public bool ByRequest => Track.IsRequested;

    /// <summary>Raised when mute or solo changes, so the editor tells the preview.</summary>
    public event Action<MusicTrackViewModel>? MixChanged;

    partial void OnMutedChanged(bool value) => MixChanged?.Invoke(this);

    partial void OnSoloedChanged(bool value) => MixChanged?.Invoke(this);

    public static double ToDecibels(float gain) => gain <= 0.0001f ? -60 : 20 * Math.Log10(gain);

    public static float FromDecibels(double decibels) => decibels <= -60 ? 0 : (float)Math.Pow(10, decibels / 20);

    /// <summary>The track's own gain in decibels, which the header's bar and the mixer's fader set.</summary>
    public double GainDb
    {
        get => ToDecibels(Track.Gain);
        set
        {
            double clamped = Math.Clamp(value, -60, 6);
            Track.Gain = FromDecibels(clamped);
            OnPropertyChanged();
            OnPropertyChanged(nameof(GainText));
            _edited();
        }
    }

    public string GainText => $"{GainDb:0.0}";

    /// <summary>The name of the icon for when the layer plays: asked for, in some hours of the day, or always.</summary>
    public string PlaysIcon
    {
        get
        {
            if (Track.IsRequested)
            {
                return "Tag";
            }

            return Hours.Length > 0 ? "Clock" : "Always";
        }
    }

    /// <summary>When the layer plays: the tags that ask for it, the hours it keeps, or always.</summary>
    public string PlaysText
    {
        get
        {
            if (Track.Triggers.Count > 0)
            {
                return string.Join(", ", Track.Triggers.Select(trigger => trigger.Name));
            }

            if (Track.ByRequest)
            {
                return "On request";
            }

            string hours = Hours;
            return hours.Length > 0 ? hours : "Always";
        }
    }

    /// <summary>How the layer comes in: thrown about, with nothing to loop, or on a line of the grid; empty where the arrangement has no grid.</summary>
    public string StartText
    {
        get
        {
            if (IsScatter)
            {
                return "scattered";
            }

            if (Track.Loop.Length == 0)
            {
                return "no loop";
            }

            if (_cueTempo() <= 0)
            {
                return string.Empty;
            }

            return Track.Start == MusicQuantize.None ? "At once" : Track.Start.ToString();
        }
    }

    private string Hours
    {
        get
        {
            IReadOnlyList<RehearsalSpan> spans = MusicArrangement.GetHours(Track.When);
            return DescribeHours(spans);
        }
    }

    /// <summary>The hours of the day a layer keeps, in words; empty for the whole day.</summary>
    public static string DescribeHours(IReadOnlyList<RehearsalSpan> spans)
    {
        double day = MusicArrangement.DayHours;
        if (spans.Count == 0)
        {
            return "Never";
        }

        if (spans.Count == 1)
        {
            RehearsalSpan span = spans[0];
            if (span.On <= 0 && span.Off >= day)
            {
                return string.Empty;
            }

            if (span.Off >= day)
            {
                return $"From {WordHour(span.On)}";
            }

            if (span.On <= 0)
            {
                return $"Until {WordHour(span.Off)}";
            }

            return $"{WordHour(span.On)} to {WordHour(span.Off)}";
        }

        if (spans.Count == 2 && spans[0].On <= 0 && spans[1].Off >= day)
        {
            return $"{WordHour(spans[1].On)} to {WordHour(spans[0].Off)}";
        }

        return "Some hours";
    }

    private static string WordHour(double hour) => $"{(int)hour:00}:00";

    /// <summary>Whether its loop is played at another tempo than the cue's, so it is stretched.</summary>
    public bool IsStretched => Track.Tempo > 0 && _cueTempo() > 0 && Math.Abs(Track.Tempo - _cueTempo()) > 0.01f;

    public bool IsScatter => Track.Scatter.Length > 0;

    public void Refresh(MusicCue cue, ClipCatalog catalog, IReadOnlyList<RehearsalSpan> spans, double totalBars)
    {
        Lengths = MusicArrangement.Measure(cue, Track, catalog);
        Clips = MusicArrangement.Plan(cue, Track, Lengths, spans, totalBars);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ByRequest));
        OnPropertyChanged(nameof(IsScatter));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(BusChoice));
        OnPropertyChanged(nameof(BusText));
        OnPropertyChanged(nameof(PlaysIcon));
        OnPropertyChanged(nameof(PlaysText));
        OnPropertyChanged(nameof(StartText));
        OnPropertyChanged(nameof(IsStretched));
        OnPropertyChanged(nameof(GainDb));
        OnPropertyChanged(nameof(GainText));
    }
}
