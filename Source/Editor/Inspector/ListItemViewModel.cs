using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>One string of a list field; editing it writes the whole list.</summary>
public sealed partial class ListItemViewModel(ListFieldViewModel list, string text, IReadOnlyList<string> suggestions) : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    /// <summary>The content files the item may name, when the list names files.</summary>
    public IReadOnlyList<string> Suggestions { get; } = suggestions;

    partial void OnTextChanged(string value) => list.WriteItems();
}
