using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>
/// A music cue as a DAW view: the tempo and meter, the rehearsal that asks for tracks, a lane per track with its clips, and the
/// selected track's values; <paramref name="edited"/> follows each change, and the lanes are laid out again after it.
/// </summary>
public sealed partial class MusicEditorViewModel : ObservableObject
{
    public const double DefaultTotalBars = 24;

    private readonly MusicCue _cue;
    private readonly Action _edited;
    private readonly ClipCatalog _catalog;
    private readonly JsonTypeInfo _trackType;
    private readonly FindContentFiles? _files;
    private readonly List<RehearsalSpan> _spans = [new RehearsalSpan(4, 12)];

    [ObservableProperty]
    private MusicTrackViewModel? _selected;

    [ObservableProperty]
    private double _totalBars = DefaultTotalBars;

    [ObservableProperty]
    private double _barWidth = MusicLayout.DefaultBarWidth;

    public MusicEditorViewModel(string title, MusicCue cue, JsonTypeInfo cueType, JsonTypeInfo trackType, Action edited, ClipCatalog catalog, FindContentFiles? files = null)
    {
        Title = title;
        _cue = cue;
        _edited = () =>
        {
            edited();
            Refresh();
        };
        _catalog = catalog;
        _trackType = trackType;
        _files = files;
        Cue = new DataObjectViewModel(title, cue, cueType, _edited, files, ["tracks"]);
        Tracks = [];
        foreach (MusicTrack track in cue.Tracks)
        {
            Tracks.Add(new MusicTrackViewModel(track, trackType, _edited, files));
        }

        Selected = Tracks.FirstOrDefault();
        Refresh();
    }

    public string Title { get; }

    public ClipCatalog Catalog => _catalog;

    public DataObjectViewModel Cue { get; }

    public ObservableCollection<MusicTrackViewModel> Tracks { get; }

    /// <summary>The spans of the rehearsal in which requested tracks are wanted; they stand for the game's areas.</summary>
    public IReadOnlyList<RehearsalSpan> Spans => _spans;

    public int PhraseBars => _cue.PhraseBars;

    public double BarSeconds => MusicArrangement.GetBarSeconds(_cue);

    public bool HasTracks => Tracks.Count > 0;

    public bool HasRehearsal => Tracks.Any(track => track.ByRequest);

    public void SetSpan(int index, RehearsalSpan span)
    {
        _spans[index] = span;
        Layout();
    }

    [RelayCommand]
    private void AddTrack()
    {
        MusicTrack track = new() { Name = $"Track {Tracks.Count + 1}" };
        _cue.Tracks.Add(track);
        MusicTrackViewModel model = new(track, _trackType, _edited, _files);
        Tracks.Add(model);
        Selected = model;
        _edited();
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
        if (Tracks.Count > 0)
        {
            Selected = Tracks[Math.Min(index, Tracks.Count - 1)];
        }
        else
        {
            Selected = null;
        }

        _edited();
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
        _edited();
    }

    public void Refresh()
    {
        _catalog.Forget();
        Layout();
    }

    private void Layout()
    {
        foreach (MusicTrackViewModel track in Tracks)
        {
            track.Refresh(_cue, _catalog, _spans, TotalBars);
        }

        OnPropertyChanged(nameof(BarSeconds));
        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(HasRehearsal));
    }
}
