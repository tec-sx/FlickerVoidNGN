using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A continue point of the cue as a row of the Cue tab: its name and the bar it picks up from.</summary>
public sealed partial class ContinuePointViewModel(MusicContinuePoint point, Action changed) : ObservableObject
{
    public MusicContinuePoint Point { get; } = point;

    public string Name
    {
        get => Point.Name;
        set
        {
            Point.Name = value;
            OnPropertyChanged();
            changed();
        }
    }

    public decimal Bar
    {
        get => (decimal)Point.Bar;
        set
        {
            Point.Bar = (float)Math.Max(1, value);
            OnPropertyChanged();
            changed();
        }
    }
}
