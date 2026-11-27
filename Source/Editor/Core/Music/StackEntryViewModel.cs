using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A cue on the stack the preview keeps: its name and where it is, heard or held.</summary>
public sealed partial class StackEntryViewModel(string name) : ObservableObject
{
    public string Name { get; } = name;

    public string Title => System.IO.Path.GetFileNameWithoutExtension(Name);

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private bool _isTop;
}
