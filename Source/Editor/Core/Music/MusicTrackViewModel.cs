using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>One track of a cue as a lane: its clips on the grid, and its values in sections for the side panel.</summary>
public sealed partial class MusicTrackViewModel : ObservableObject
{
    private static readonly string[] AllNames = ["name", "loop", "tempo", "bars", "fillIn", "fillOut", "gain", "byRequest", "when", "start", "stop", "phase", "fadeIn", "fadeOut", "scatter", "everyMin", "everyMax", "nearDistance", "farDistance"];

    private static readonly string[][] Sections =
    [
        ["name", "byRequest", "when"],
        ["loop", "fillIn", "fillOut", "tempo", "bars"],
        ["start", "stop", "phase", "fadeIn", "fadeOut"],
        ["gain"],
        ["scatter", "everyMin", "everyMax", "nearDistance", "farDistance"],
    ];

    private static readonly string[] SectionTitles = ["Track", "Clips", "Timing", "Mix", "Scatter"];

    private readonly Action _edited;
    private readonly Func<float> _cueTempo;

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

    public MusicTrackViewModel(MusicTrack track, JsonTypeInfo typeInfo, Action edited, FindContentFiles? files, Func<float>? cueTempo = null)
    {
        Track = track;
        _edited = edited;
        _cueTempo = cueTempo ?? (() => 0);
        List<DataObjectViewModel> forms = [];
        for (int index = 0; index < Sections.Length; index++)
        {
            string[] shown = Sections[index];
            string[] hidden = AllNames.Where(name => !shown.Contains(name)).ToArray();
            forms.Add(new DataObjectViewModel(SectionTitles[index], track, typeInfo, edited, files, hidden));
        }

        Forms = forms;
    }

    public MusicTrack Track { get; }

    public IReadOnlyList<DataObjectViewModel> Forms { get; }

    public string Title => Track.Name.Length > 0 ? Track.Name : "Track";

    public bool ByRequest => Track.ByRequest;

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

    /// <summary>When the track plays: always, only when asked, or thrown around the listener.</summary>
    public string PlaysText
    {
        get
        {
            if (IsScatter)
            {
                return "Scattered";
            }

            return ByRequest ? "On request" : "Always";
        }
    }

    public string StartText => Track.Start == MusicQuantize.None ? "At once" : Track.Start.ToString();

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
        OnPropertyChanged(nameof(PlaysText));
        OnPropertyChanged(nameof(StartText));
        OnPropertyChanged(nameof(IsStretched));
        OnPropertyChanged(nameof(GainDb));
        OnPropertyChanged(nameof(GainText));
    }
}
