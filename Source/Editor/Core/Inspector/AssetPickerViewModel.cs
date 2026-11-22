using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// Picks a content file for a value, like Unreal's asset picker: opening it lists the files the value may name, the search keeps
/// those whose path holds its text, and a choice becomes the current file.
/// </summary>
public sealed partial class AssetPickerViewModel : ObservableObject
{
    private const string NoneName = "None";

    private readonly Func<IReadOnlyList<string>> _find;
    private readonly Action<string?> _chosen;
    private IReadOnlyList<string> _assets = [];

    public AssetPickerViewModel(Func<IReadOnlyList<string>> find, bool acceptsNone, Action<string?> chosen)
    {
        _find = find;
        _chosen = chosen;
        AcceptsNone = acceptsNone;
    }

    public bool AcceptsNone { get; }

    /// <summary>The file's name without its folder, or None.</summary>
    public string Name
    {
        get
        {
            if (string.IsNullOrEmpty(Current))
            {
                return NoneName;
            }

            return Path.GetFileName(Current);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    public partial string? Current { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> Matches { get; set; } = [];

    [RelayCommand]
    public void Choose(string? path)
    {
        Current = path;
        IsOpen = false;
        _chosen(path);
    }

    [RelayCommand(CanExecute = nameof(AcceptsNone))]
    public void ChooseNone()
    {
        Choose(null);
    }

    /// <summary>Reads the files again each time it opens, so files made since show up.</summary>
    partial void OnIsOpenChanged(bool value)
    {
        Search = "";
        if (value)
        {
            _assets = _find();
        }

        Filter();
    }

    partial void OnSearchChanged(string value) => Filter();

    private void Filter()
    {
        List<string> matches = [];
        foreach (string asset in _assets)
        {
            if (asset.Contains(Search, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(asset);
            }
        }

        Matches = matches;
    }
}
