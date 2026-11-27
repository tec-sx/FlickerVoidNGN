using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A mix state of the project as a row of the mixer dock: turned on to hear what the game does to the buses while it holds.</summary>
public sealed partial class MixStateViewModel(MixState state, Action<MixStateViewModel> toggled) : ObservableObject
{
    public MixState State { get; } = state;

    public string Name => State.Name;

    [ObservableProperty]
    private bool _isOn;

    partial void OnIsOnChanged(bool value) => toggled(this);

    /// <summary>What it does, in words: each duck as the bus, its dulling and its gain in decibels.</summary>
    public string Summary
    {
        get
        {
            List<string> parts = [];
            foreach (MixDuck duck in State.Ducks)
            {
                string part = $"{duck.Bus}";
                if (duck.Cutoff < MixerCommand.NoCutoff)
                {
                    part += $" dulled to {duck.Cutoff / 1000:0.0} kHz";
                }

                if (duck.Gain < 0.999f)
                {
                    part += $", {MusicTrackViewModel.ToDecibels(duck.Gain):0} dB";
                }

                parts.Add(part);
            }

            return parts.Count == 0 ? "every bus as set" : string.Join(" · ", parts);
        }
    }
}
