using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A track of the Live dock: whether it sounds and, in words, what it plays on or waits for.</summary>
public sealed partial class LiveRowViewModel(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private bool _isOn;
}
