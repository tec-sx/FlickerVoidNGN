using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Audio.Music;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A section of the song as a row of the Cue tab: its name and its length in bars.</summary>
public sealed partial class SectionViewModel(MusicSection section, Action changed) : ObservableObject
{
    public MusicSection Section { get; } = section;

    public string Name
    {
        get => Section.Name;
        set
        {
            Section.Name = value;
            OnPropertyChanged();
            changed();
        }
    }

    public decimal Bars
    {
        get => Section.Bars;
        set
        {
            Section.Bars = (int)Math.Clamp(value, 1, 256);
            OnPropertyChanged();
            changed();
        }
    }
}
