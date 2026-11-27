using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>One bus of the mixer dock: its fader (the volume heard while editing), mute and solo, and its meter.</summary>
public sealed partial class BusStripViewModel(AudioBus bus, Action changed) : ObservableObject
{
    public AudioBus Bus { get; } = bus;

    public string Name => Bus.ToString();

    [ObservableProperty]
    private double _volume = 1;

    [ObservableProperty]
    private bool _muted;

    [ObservableProperty]
    private bool _soloed;

    [ObservableProperty]
    private double _level;

    public string VolumeText => $"{MusicTrackViewModel.ToDecibels((float)Volume):0.0}";

    partial void OnVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(VolumeText));
        changed();
    }

    partial void OnMutedChanged(bool value) => changed();

    partial void OnSoloedChanged(bool value) => changed();
}
