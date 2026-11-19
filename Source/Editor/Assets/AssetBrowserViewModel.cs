using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>
/// The asset browser: the content folder's tree, the files of the selected folder, and a kind filter and search that look
/// through everything below it. Opening a file raises <see cref="Opened"/>, which the editor answers by kind.
/// </summary>
public sealed partial class AssetBrowserViewModel : ObservableObject
{
    private readonly AssetCatalog _catalog;

    public AssetBrowserViewModel(AssetCatalog catalog)
    {
        _catalog = catalog;
        List<AssetFilter> filters = [new AssetFilter("All", null)];
        foreach (AssetKind kind in Enum.GetValues<AssetKind>())
        {
            filters.Add(new AssetFilter(kind.ToString(), kind));
        }

        Filters = filters;
        SelectedFilter = filters[0];
        Refresh();
    }

    /// <summary>Raised when a file is opened, by double-click or Enter.</summary>
    public event Action<AssetEntry>? Opened;

    public IReadOnlyList<AssetFilter> Filters { get; }

    /// <summary>The content folder, as the one root of the tree.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetFolder> Folders { get; set; } = [];

    [ObservableProperty]
    public partial AssetFolder? SelectedFolder { get; set; }

    [ObservableProperty]
    public partial AssetFilter SelectedFilter { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<AssetEntry> Assets { get; set; } = [];

    /// <summary>Reads the folders and files again, keeping the selected folder when it still exists.</summary>
    [RelayCommand]
    public void Refresh()
    {
        string? selected = SelectedFolder?.Path;
        AssetFolder root = _catalog.GetFolders();
        AssetFolder named = root with { Name = "Content" };
        Folders = [named];
        AssetFolder? found = null;
        if (selected is not null)
        {
            found = FindFolder(named, selected);
        }

        SelectedFolder = found ?? named;
        FindAssets();
    }

    [RelayCommand]
    public void Open(AssetEntry? asset)
    {
        if (asset is not null)
        {
            Opened?.Invoke(asset);
        }
    }

    partial void OnSelectedFolderChanged(AssetFolder? value) => FindAssets();

    partial void OnSelectedFilterChanged(AssetFilter value) => FindAssets();

    partial void OnSearchTextChanged(string value) => FindAssets();

    private void FindAssets()
    {
        string folder = SelectedFolder?.Path ?? "";
        AssetKind? kind = SelectedFilter?.Kind;
        string text = SearchText ?? "";
        bool recursive = kind is not null || text.Length > 0;
        Assets = _catalog.Find(folder, recursive, kind, text);
    }

    private static AssetFolder? FindFolder(AssetFolder folder, string path)
    {
        if (folder.Path == path)
        {
            return folder;
        }

        foreach (AssetFolder child in folder.Children)
        {
            AssetFolder? found = FindFolder(child, path);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
