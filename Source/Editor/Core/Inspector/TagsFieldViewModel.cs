using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Tags;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// A tag, or a list or array of tags, as chips with an x each and a "+ Tag" chip that opens the project's tag picker; a value
/// holding one tag shows at most one chip.
/// </summary>
public sealed partial class TagsFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    public ObservableCollection<string> Tags { get; } = [];

    /// <summary>Whether the value holds one tag rather than a list.</summary>
    public bool IsSingle => Property.Type == typeof(Tag);

    /// <summary>Whether the "+ Tag" chip shows: always for a list, while empty for one tag.</summary>
    public bool CanAdd => !IsReadOnly && (!IsSingle || Tags.Count == 0);

    /// <summary>The picker over the catalog for this value, the value's own tags added to the catalog's tree.</summary>
    public TagPickerViewModel OpenPicker(TagCatalog catalog)
    {
        foreach (string tag in Tags)
        {
            if (!catalog.Contains(tag))
            {
                catalog.Add(tag);
            }
        }

        return new TagPickerViewModel(catalog, () => Tags, WriteTags, IsSingle);
    }

    [RelayCommand]
    private void Remove(string? tag)
    {
        if (tag is null)
        {
            return;
        }

        List<string> left = [.. Tags.Where(held => !string.Equals(held, tag, StringComparison.OrdinalIgnoreCase))];
        WriteTags(left);
    }

    private void WriteTags(IReadOnlyList<string> names)
    {
        if (IsReadOnly)
        {
            return;
        }

        ShowTags(names);
        Tag[] tags = [.. names.Select(name => new Tag(name))];
        if (IsSingle)
        {
            Tag one = Tag.None;
            if (tags.Length > 0)
            {
                one = tags[0];
            }

            Write(one);
            return;
        }

        if (Property.Type == typeof(Tag[]))
        {
            Write(tags);
            return;
        }

        Write(new List<Tag>(tags));
    }

    protected override void Show(object? value)
    {
        List<string> names = [];
        if (value is Tag one && one.IsValid)
        {
            names.Add(one.Name);
        }
        else if (value is IEnumerable<Tag> many)
        {
            names.AddRange(many.Where(tag => tag.IsValid).Select(tag => tag.Name));
        }

        ShowTags(names);
    }

    private void ShowTags(IReadOnlyList<string> names)
    {
        if (names.SequenceEqual(Tags))
        {
            return;
        }

        Tags.Clear();
        foreach (string name in names)
        {
            Tags.Add(name);
        }

        OnPropertyChanged(nameof(CanAdd));
    }
}
