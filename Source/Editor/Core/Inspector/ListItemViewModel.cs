using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>One string of a list field, picked with an <see cref="AssetPickerViewModel"/> when the list names content files; editing it writes the whole list.</summary>
public sealed partial class ListItemViewModel : ObservableObject
{
    private readonly ListFieldViewModel _list;

    public ListItemViewModel(ListFieldViewModel list, string text, Func<IReadOnlyList<string>>? find)
    {
        _list = list;
        Text = text;
        if (find is not null)
        {
            Picker = new AssetPickerViewModel(find, acceptsNone: false, Choose) { Current = text };
        }
    }

    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>The picker of the content files the item may name, or null when the list holds plain strings.</summary>
    public AssetPickerViewModel? Picker { get; }

    public bool IsText => Picker is null;

    partial void OnTextChanged(string value) => _list.WriteItems();

    private void Choose(string? path)
    {
        Text = path ?? "";
    }
}
