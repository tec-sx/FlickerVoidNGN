using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Core.Tags;

/// <summary>
/// Picks tags from the project's tree, as Unreal's tag picker: a checkbox per tag (a parent shows a dash when something below it
/// is picked), folding, a search over the whole tree, and adding, renaming and removing tags in place; a tag in use is removed only
/// once confirmed.
/// </summary>
public sealed partial class TagPickerViewModel : ObservableObject
{
    private readonly TagCatalog _catalog;
    private readonly Func<IReadOnlyList<string>> _picked;
    private readonly Action<IReadOnlyList<string>> _pick;
    private readonly bool _single;
    private readonly HashSet<string> _folded = new(StringComparer.OrdinalIgnoreCase);
    private string? _removing;

    public TagPickerViewModel(TagCatalog catalog, Func<IReadOnlyList<string>> picked, Action<IReadOnlyList<string>> pick, bool single = false)
    {
        _catalog = catalog;
        _picked = picked;
        _pick = pick;
        _single = single;
        FoldUnpicked();
        Refresh();
    }

    public ObservableCollection<TagPickerRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial string NewTag { get; set; } = "";

    /// <summary>The question asked before a tag in use is removed; null while nothing is asked.</summary>
    [ObservableProperty]
    public partial string? Asking { get; set; }

    /// <summary>Why the last add, rename or removal did not happen; null when it did.</summary>
    [ObservableProperty]
    public partial string? Problem { get; set; }

    public string PickedText => $"{_picked().Count} picked";

    /// <summary>Shows the tree again, after the catalog or the picked tags changed elsewhere.</summary>
    public void Refresh()
    {
        IReadOnlyList<string> picked = _picked();
        Rows.Clear();
        foreach (TagRow row in _catalog.GetRows(Search, _folded))
        {
            bool on = picked.Contains(row.Path, StringComparer.OrdinalIgnoreCase);
            bool part = !on && picked.Any(tag => IsBelow(tag, row.Path));
            string note = NoteOf(row);
            Rows.Add(new TagPickerRowViewModel(row, on, part, note));
        }

        OnPropertyChanged(nameof(PickedText));
    }

    partial void OnSearchChanged(string value) => Refresh();

    [RelayCommand]
    private void Toggle(TagPickerRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        List<string> picked = [.. _picked()];
        int at = picked.FindIndex(tag => string.Equals(tag, row.Path, StringComparison.OrdinalIgnoreCase));
        if (at >= 0)
        {
            picked.RemoveAt(at);
        }
        else if (_single)
        {
            picked = [row.Path];
        }
        else
        {
            picked.Add(row.Path);
        }

        _pick(picked);
        Refresh();
    }

    [RelayCommand]
    private void Fold(TagPickerRowViewModel? row)
    {
        if (row is null || !row.HasChildren)
        {
            return;
        }

        if (!_folded.Remove(row.Path))
        {
            _folded.Add(row.Path);
        }

        Refresh();
    }

    /// <summary>Adds the tag typed in the footer, with its parents, picks it and opens the branch it is in.</summary>
    [RelayCommand]
    private void AddTag()
    {
        string name = NewTag.Trim().TrimEnd('.');
        if (name.Length == 0)
        {
            return;
        }

        if (!_catalog.Contains(name) && !_catalog.Add(name))
        {
            Problem = $"{name} is not a tag name: parts joined by dots, none empty.";
            return;
        }

        Problem = null;
        NewTag = "";
        OpenTo(name);
        if (!_picked().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            List<string> picked = _single ? [name] : [.. _picked(), name];
            _pick(picked);
        }

        Refresh();
    }

    [RelayCommand]
    private void AddBelow(TagPickerRowViewModel? row)
    {
        if (row is not null)
        {
            NewTag = $"{row.Path}.";
        }
    }

    [RelayCommand]
    private void StartRename(TagPickerRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        foreach (TagPickerRowViewModel other in Rows)
        {
            other.IsRenaming = false;
        }

        row.RenameText = row.Path;
        row.IsRenaming = true;
    }

    [RelayCommand]
    private void CancelRename(TagPickerRowViewModel? row)
    {
        if (row is not null)
        {
            row.IsRenaming = false;
        }
    }

    /// <summary>Renames the tag and everything below it, in the catalog, the project's files and the picked tags.</summary>
    [RelayCommand]
    private void CommitRename(TagPickerRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        string from = row.Path;
        string to = row.RenameText.Trim();
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            row.IsRenaming = false;
            return;
        }

        if (!_catalog.Rename(from, to, out string? why))
        {
            Problem = why;
            return;
        }

        Problem = null;
        List<string> picked = [.. _picked().Select(tag => IsAtOrBelow(tag, from) ? to + tag[from.Length..] : tag)];
        _pick(picked);
        OpenTo(to);
        Refresh();
    }

    /// <summary>Removes an unused tag at once; asks first about one the project's files hold.</summary>
    [RelayCommand]
    private void Remove(TagPickerRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        int uses = _catalog.UsesBelow(row.Path);
        if (uses == 0)
        {
            RemoveNow(row.Path);
            return;
        }

        _removing = row.Path;
        Asking = $"{row.Path} is used {Times(uses)}. Remove it and the tags below it from every file?";
    }

    [RelayCommand]
    private void ConfirmRemove()
    {
        if (_removing is { } name)
        {
            RemoveNow(name);
        }
    }

    [RelayCommand]
    private void CancelRemove()
    {
        _removing = null;
        Asking = null;
    }

    private void RemoveNow(string name)
    {
        _removing = null;
        Asking = null;
        if (!_catalog.Remove(name, out string? why))
        {
            Problem = why;
            return;
        }

        Problem = null;
        List<string> picked = [.. _picked().Where(tag => !IsAtOrBelow(tag, name))];
        _pick(picked);
        Refresh();
    }

    private string NoteOf(TagRow row)
    {
        int uses = _catalog.UsesOf(row.Path);
        if (row.IsFolded && Search.Length == 0)
        {
            int below = _catalog.CountBelow(row.Path);
            return below.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (uses > 0)
        {
            return $"used {Times(uses)}";
        }

        return "";
    }

    private static string Times(int count)
    {
        if (count == 1)
        {
            return "once";
        }

        return $"{count} times";
    }

    /// <summary>Folds every parent that is not picked and holds nothing picked, so the picker opens on what the value holds.</summary>
    private void FoldUnpicked()
    {
        IReadOnlyList<string> picked = _picked();
        foreach (TagRow row in _catalog.GetRows("", []))
        {
            bool holds = picked.Any(tag => IsAtOrBelow(tag, row.Path));
            if (row.HasChildren && !holds)
            {
                _folded.Add(row.Path);
            }
        }
    }

    private void OpenTo(string name)
    {
        int dot = name.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = name[..dot];
            _folded.Remove(parent);
            dot = parent.LastIndexOf('.');
        }
    }

    private static bool IsBelow(string tag, string parent)
    {
        return tag.Length > parent.Length && tag[parent.Length] == '.' && tag.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAtOrBelow(string tag, string parent)
    {
        return string.Equals(tag, parent, StringComparison.OrdinalIgnoreCase) || IsBelow(tag, parent);
    }
}
