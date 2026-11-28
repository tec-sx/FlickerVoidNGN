using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Tags;

/// <summary>A tag of the picker's tree: its name, how deep it sits, whether it is picked or holds a picked tag, and its note.</summary>
public sealed partial class TagPickerRowViewModel(TagRow row, bool isOn, bool isPart, string note) : ObservableObject
{
    private const double Indentation = 15;

    private const double Start = 8;

    public string Path => row.Path;

    public string Name => row.Name;

    public bool HasChildren => row.HasChildren;

    public bool IsFolded => row.IsFolded;

    /// <summary>The row's left padding: 8 px and 15 px a level.</summary>
    public double Indent => Start + (row.Depth * Indentation);

    public bool IsOn => isOn;

    /// <summary>Whether a tag below it is picked while it is not.</summary>
    public bool IsPart => isPart;

    /// <summary>"used 2 times", or the number of tags below a folded parent; empty for none.</summary>
    public string Note => note;

    public bool HasNote => note.Length > 0;

    [ObservableProperty]
    public partial bool IsRenaming { get; set; }

    [ObservableProperty]
    public partial string RenameText { get; set; } = "";
}
