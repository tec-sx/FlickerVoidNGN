using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>One track of a cue as a lane: its clips on the grid, and its values in sections for the side panel.</summary>
public sealed partial class MusicTrackViewModel : ObservableObject
{
    private static readonly string[] AllNames = ["name", "loop", "bars", "fillIn", "fillOut", "gain", "byRequest", "when", "start", "stop", "phase", "fadeIn", "fadeOut", "scatter", "everyMin", "everyMax", "nearDistance", "farDistance"];

    private static readonly string[][] Sections =
    [
        ["name", "byRequest", "when"],
        ["loop", "fillIn", "fillOut", "bars"],
        ["start", "stop", "phase", "fadeIn", "fadeOut"],
        ["gain"],
        ["scatter", "everyMin", "everyMax", "nearDistance", "farDistance"],
    ];

    private static readonly string[] SectionTitles = ["Track", "Clips", "Timing", "Mix", "Scatter"];

    [ObservableProperty]
    private IReadOnlyList<ArrangementClip> _clips = [];

    [ObservableProperty]
    private TrackLengths _lengths;

    public MusicTrackViewModel(MusicTrack track, JsonTypeInfo typeInfo, Action edited, FindContentFiles? files)
    {
        Track = track;
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

    public bool IsScatter => Track.Scatter.Length > 0;

    public void Refresh(MusicCue cue, ClipCatalog catalog, IReadOnlyList<RehearsalSpan> spans, double totalBars)
    {
        Lengths = MusicArrangement.Measure(cue, Track, catalog);
        Clips = MusicArrangement.Plan(cue, Track, Lengths, spans, totalBars);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ByRequest));
        OnPropertyChanged(nameof(IsScatter));
    }
}
