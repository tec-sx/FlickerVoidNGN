using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>
/// The asset browser: the content folder's tree, the files of the selected folder, and a kind filter and search that look
/// through everything below it. Opening a file raises <see cref="Opened"/>, which the editor answers by kind. It creates,
/// renames and deletes files and folders through the content manager, so their source files follow in a development build.
/// </summary>
public sealed partial class AssetBrowserViewModel : ObservableObject
{
    private const string SchemaFolder = ".schemas/";

    private readonly IContentManager _content;
    private readonly List<AssetCatalog> _catalogs;

    public AssetBrowserViewModel(IContentManager content)
    {
        _content = content;
        _catalogs = CreateCatalogs(content);
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

    /// <summary>The roots of the tree: the game's source folders in a development build, else the content folder.</summary>
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

    /// <summary>The file renaming and deleting work on; with none, renaming works on the selected folder.</summary>
    [ObservableProperty]
    public partial AssetEntry? SelectedAsset { get; set; }

    /// <summary>Why the last file operation was refused, or empty.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "";

    /// <summary>Reads the folders and files again, keeping the selected folder when it still exists.</summary>
    [RelayCommand]
    public void Refresh()
    {
        string? selected = SelectedFolder?.Path;
        List<AssetFolder> roots = [];
        foreach (AssetCatalog catalog in _catalogs)
        {
            AssetFolder root = catalog.GetFolders();
            roots.Add(root);
        }

        Folders = roots;
        SelectFolder(selected ?? roots[0].Path);
    }

    [RelayCommand]
    public void Open(AssetEntry? asset)
    {
        if (asset is not null)
        {
            Opened?.Invoke(asset);
        }
    }

    [RelayCommand]
    public void CreateFolder()
    {
        string path = FindFreePath("New Folder", "");
        _content.CreateFolder(path);
        Refresh();
        SelectFolder(path);
    }

    [RelayCommand]
    public void CreateScene()
    {
        CreateFile("New Scene", ".fvscene", "scene", "\"entities\": []");
    }

    [RelayCommand]
    public void CreateMaterial()
    {
        CreateFile("New Material", ".fvmat", "material", "\"parameters\": {}");
    }

    [RelayCommand]
    public void CreateMood()
    {
        CreateFile("New Mood", ".fvmood", "mood", null);
    }

    /// <summary>Renames the selected file, keeping its extensions, or the selected folder when no file is selected.</summary>
    [RelayCommand]
    public void Rename(string? name)
    {
        Status = "";
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['/', '\\']) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Status = $"'{name}' can't be a file name.";
            return;
        }

        if (SelectedAsset is { } asset)
        {
            string extension = asset.Path[(asset.Path.LastIndexOf('/') + 1 + asset.Name.Length)..];
            string newPath = Combine(GetFolder(asset.Path), name + extension);
            if (Move(asset.Path, newPath))
            {
                FindAssets();
                SelectedAsset = Assets.FirstOrDefault(entry => entry.Path == newPath);
            }

            return;
        }

        if (SelectedFolder is { Path.Length: > 0 } folder)
        {
            string newPath = Combine(GetFolder(folder.Path), name);
            if (Move(folder.Path, newPath))
            {
                Refresh();
                SelectFolder(newPath);
            }
        }
    }

    /// <summary>Deletes the selected file, or the selected folder with everything in it when no file is selected.</summary>
    [RelayCommand]
    public void Delete()
    {
        Status = "";
        if (SelectedAsset is { } asset)
        {
            _content.Delete(asset.Path);
            SelectedAsset = null;
            FindAssets();
            return;
        }

        if (SelectedFolder is { Path.Length: > 0 } folder)
        {
            string parent = GetFolder(folder.Path);
            _content.Delete(folder.Path);
            Refresh();
            SelectFolder(parent);
        }
    }

    partial void OnSelectedFolderChanged(AssetFolder? value) => FindAssets();

    partial void OnSelectedFilterChanged(AssetFilter value) => FindAssets();

    partial void OnSearchTextChanged(string value) => FindAssets();

    private void FindAssets()
    {
        if (Folders.Count == 0)
        {
            return;
        }

        string folder = SelectedFolder?.Path ?? Folders[0].Path;
        AssetKind? kind = SelectedFilter?.Kind;
        string text = SearchText ?? "";
        bool recursive = kind is not null || text.Length > 0;
        AssetCatalog catalog = FindCatalog(folder);
        Assets = catalog.Find(folder, recursive, kind, text);
    }

    private void CreateFile(string name, string extension, string schema, string? body)
    {
        string folder = SelectedFolder?.Path ?? "";
        string path = FindFreePath(name, extension);
        AssetCatalog catalog = FindCatalog(folder);
        string local = folder[catalog.Prefix.Length..].TrimStart('/');
        string schemaPath = GetSchemaPath(local, schema);
        StringBuilder text = new();
        text.Append("{\n");
        text.Append($"  \"$schema\": \"{schemaPath}\"");
        if (body is not null)
        {
            text.Append($",\n  {body}");
        }

        text.Append("\n}\n");
        string contents = text.ToString();
        byte[] bytes = Encoding.UTF8.GetBytes(contents);
        _content.Write(path, bytes);
        SearchText = "";
        FindAssets();
        SelectedAsset = Assets.FirstOrDefault(entry => entry.Path == path);
    }

    /// <summary>The name with the extension in the selected folder, numbered from 2 while it is taken.</summary>
    private string FindFreePath(string name, string extension)
    {
        string folder = SelectedFolder?.Path ?? "";
        string path = Combine(folder, name + extension);
        int number = 2;
        while (Exists(path))
        {
            path = Combine(folder, $"{name} {number}{extension}");
            number++;
        }

        return path;
    }

    private bool Move(string path, string newPath)
    {
        if (Exists(newPath))
        {
            Status = $"{newPath} already exists.";
            return false;
        }

        _content.Move(path, newPath);
        return true;
    }

    private bool Exists(string path)
    {
        string fullPath = _content.GetFullPath(path);
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }

    private void SelectFolder(string path)
    {
        AssetFolder? found = null;
        foreach (AssetFolder root in Folders)
        {
            found ??= FindFolder(root, path);
        }

        SelectedFolder = found ?? Folders[0];
        FindAssets();
    }

    /// <summary>The catalog whose root holds the content path, the one with the longest prefix when roots nest.</summary>
    private AssetCatalog FindCatalog(string path)
    {
        AssetCatalog found = _catalogs[0];
        foreach (AssetCatalog catalog in _catalogs)
        {
            if (catalog.Holds(path) && (!found.Holds(path) || catalog.Prefix.Length > found.Prefix.Length))
            {
                found = catalog;
            }
        }

        return found;
    }

    /// <summary>One catalog per game source folder the build manifest names, else one over the content folder.</summary>
    private static List<AssetCatalog> CreateCatalogs(IContentManager content)
    {
        List<AssetCatalog> catalogs = [];
        ContentBuildManifest? manifest = ContentBuildManifest.Find(content.RootDirectory);
        if (manifest is not null)
        {
            foreach (ContentBuildFolder folder in manifest.Folders)
            {
                if (folder.Engine)
                {
                    continue;
                }

                string relative = Path.GetRelativePath(content.RootDirectory, folder.Output);
                string prefix = relative.Replace(Path.DirectorySeparatorChar, '/');
                if (prefix == ".")
                {
                    prefix = "";
                }

                catalogs.Add(new AssetCatalog(folder.Source, prefix));
            }
        }

        if (catalogs.Count == 0)
        {
            catalogs.Add(new AssetCatalog(content.RootDirectory));
        }

        return catalogs;
    }

    /// <summary>The schema a new file in the folder (a path in its source folder) names: that source folder's .schemas, which development runs write.</summary>
    private static string GetSchemaPath(string folder, string schema)
    {
        StringBuilder path = new();
        if (folder.Length > 0)
        {
            int depth = folder.Count(static character => character == '/') + 1;
            path.Insert(0, "../", depth);
        }

        path.Append($"{SchemaFolder}{schema}.schema.json");
        return path.ToString();
    }

    private static string GetFolder(string path)
    {
        int slash = path.LastIndexOf('/');
        if (slash < 0)
        {
            return "";
        }

        return path[..slash];
    }

    private static string Combine(string folder, string name)
    {
        if (folder.Length == 0)
        {
            return name;
        }

        return $"{folder}/{name}";
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
