using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Flows;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// The asset browser: the content folder's tree, the files of the selected folder, and a kind filter and search that look
/// through everything below it. Opening a file raises <see cref="Opened"/>, which the editor answers by kind. It creates,
/// renames and deletes files and folders through the content manager, so their source files follow in a development build.
/// Given the project's code folder, it shows it as a second root, Code, where it only writes new flow node classes.
/// </summary>
public sealed partial class AssetBrowserViewModel : ObservableObject
{
    private const string SchemaFolder = ".schemas/";
    private const string LookGroup = "Look";
    private const string FlowGroup = "Flow";
    private const string DataGroup = "Data Asset";
    private const string TableGroup = "Table";

    private readonly IContentManager _content;
    private readonly List<AssetCatalog> _catalogs;
    private readonly AssetCatalog? _code;

    /// <summary>
    /// Browses the content; <paramref name="data"/> lists the data asset and table types it can create, and
    /// <paramref name="codeFolder"/> is the project's source folder, where flow node classes go.
    /// </summary>
    public AssetBrowserViewModel(IContentManager content, DataTypes? data = null, string? codeFolder = null)
    {
        _content = content;
        List<MenuChoice> choices =
        [
            new("Folder", CreateFolder, Icon: "NewFolder"),
            new("Scene", CreateScene, Icon: "Scene"),
            new("Material", CreateMaterial, LookGroup, "Materials"),
            new("Mood", CreateMood, LookGroup, "Mood"),
            new("Flow Graph", CreateFlow, FlowGroup, "Flow"),
        ];
        if (codeFolder is not null)
        {
            choices.Add(new MenuChoice("Flow Node", NewFlowNode, FlowGroup, "Code"));
        }

        if (data is not null)
        {
            foreach (string type in data.AssetTypes.Keys)
            {
                string name = DisplayNames.FromIdentifier(type);
                choices.Add(new MenuChoice(name, () => CreateData(type), DataGroup, "Data"));
            }

            foreach (string type in data.RowTypes.Keys)
            {
                string name = DisplayNames.FromIdentifier(type);
                choices.Add(new MenuChoice(name, () => CreateTable(type), TableGroup, "Table"));
            }
        }

        NewChoices = choices;
        _catalogs = AssetCatalog.ForContent(content);
        if (codeFolder is not null)
        {
            _code = AssetCatalog.ForCode(codeFolder);
            _catalogs.Add(_code);
        }

        List<AssetFilter> filters = [new AssetFilter("All", null)];
        foreach (AssetKind kind in Enum.GetValues<AssetKind>())
        {
            if (kind == AssetKind.Folder)
            {
                continue;
            }

            filters.Add(new AssetFilter(kind.ToString(), kind));
        }

        Filters = filters;
        SelectedFilter = filters[0];
        Refresh();
    }

    /// <summary>Raised when a file is opened, by double-click or Enter.</summary>
    public event Action<AssetEntry>? Opened;

    public IReadOnlyList<AssetFilter> Filters { get; }

    /// <summary>
    /// What the browser can create in the selected folder, in groups: folders and scenes, then materials and moods, flows, a data
    /// asset of each registered type and a table of each row type. Each menu showing them picks how to group them.
    /// </summary>
    public IReadOnlyList<MenuChoice> NewChoices { get; }

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

    /// <summary>What the browser shows: the selected folder's subfolders, then <see cref="Assets"/>; only the files while searching or filtering.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetEntry> Items { get; set; } = [];

    /// <summary>The file or folder item renaming, deleting and moving work on; with none, renaming works on the selected folder.</summary>
    [ObservableProperty]
    public partial AssetEntry? SelectedAsset { get; set; }

    /// <summary>The name and folder of the flow node class being created, while the browser asks for them.</summary>
    [ObservableProperty]
    public partial FlowNodePrompt? Prompt { get; set; }

    /// <summary>Why the last file operation was refused, or what it did when that isn't plain to see; else empty.</summary>
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
        if (asset is null)
        {
            return;
        }

        if (asset.Kind == AssetKind.Folder)
        {
            SelectFolder(asset.Path);
            return;
        }

        Opened?.Invoke(asset);
    }

    /// <summary>Shows the selected folder's parent; the roots stay where they are.</summary>
    [RelayCommand]
    public void Up()
    {
        if (SelectedFolder is not { } folder || Folders.Contains(folder))
        {
            return;
        }

        string parent = GetFolder(folder.Path);
        SelectFolder(parent);
    }

    /// <summary>Moves a file or folder into a folder, keeping its name; a folder never goes into itself or below it.</summary>
    public void MoveTo(AssetEntry item, string folder)
    {
        Status = "";
        if (RefuseCode(item.Path) || RefuseCode(folder))
        {
            return;
        }

        bool intoItself = folder == item.Path || folder.StartsWith(item.Path + "/", StringComparison.Ordinal);
        if (item.Kind == AssetKind.Folder && intoItself)
        {
            Status = $"{item.Name} can't go into itself.";
            return;
        }

        string name = item.Path[(item.Path.LastIndexOf('/') + 1)..];
        string newPath = Combine(folder, name);
        if (newPath == item.Path)
        {
            return;
        }

        if (Move(item.Path, newPath))
        {
            SelectedAsset = null;
            Refresh();
        }
    }

    [RelayCommand]
    public void CreateFolder()
    {
        Status = "";
        if (RefuseCode(SelectedFolder?.Path))
        {
            return;
        }

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

    [RelayCommand]
    public void CreateFlow()
    {
        CreateFile("New Flow", FlowGraph.Extension, "flow", "\"nodes\": [ { \"$type\": \"start\", \"id\": \"start\" } ],\n  \"links\": []");
    }

    /// <summary>Creates a data asset of the type registered under <paramref name="type"/>, with its default values.</summary>
    public void CreateData(string type)
    {
        string name = DisplayNames.FromIdentifier(type);
        CreateFile($"New {name}", DataAssets.AssetExtension, "data", $"\"type\": \"{type}\"");
    }

    /// <summary>Creates an empty data table of the row type registered under <paramref name="type"/>.</summary>
    public void CreateTable(string type)
    {
        string name = DisplayNames.FromIdentifier(type);
        CreateFile($"New {name} Table", DataAssets.TableExtension, "table", $"\"type\": \"{type}\",\n  \"rows\": {{}}");
    }

    /// <summary>Renames the selected file, keeping its extensions, or the selected folder when no file is selected.</summary>
    [RelayCommand]
    public void Rename(string? name)
    {
        Status = "";
        string? path = SelectedAsset?.Path ?? SelectedFolder?.Path;
        if (RefuseCode(path))
        {
            return;
        }

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
                if (asset.Kind == AssetKind.Folder)
                {
                    Refresh();
                }

                FindAssets();
                SelectedAsset = Items.FirstOrDefault(entry => entry.Path == newPath);
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
        string? path = SelectedAsset?.Path ?? SelectedFolder?.Path;
        if (RefuseCode(path))
        {
            return;
        }

        if (SelectedAsset is { } asset)
        {
            _content.Delete(asset.Path);
            SelectedAsset = null;
            if (asset.Kind == AssetKind.Folder)
            {
                Refresh();
            }

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

    /// <summary>The full path of a file or folder the browser shows, in the content or the code.</summary>
    public string GetFullPath(AssetEntry entry)
    {
        AssetCatalog catalog = FindCatalog(entry.Path);
        if (catalog.IsCode)
        {
            return catalog.GetFullPath(entry.Path);
        }

        return _content.GetFullPath(entry.Path);
    }

    /// <summary>
    /// Asks for a new flow node class's name and folder: the selected folder in the code, or else the code folder whose path
    /// ends most like the selected content folder's, or else the first folder holding a C# project.
    /// </summary>
    public void NewFlowNode()
    {
        if (_code is null)
        {
            return;
        }

        List<string> folders = [];
        AssetFolder root = _code.GetFolders();
        CollectFolders(root, folders);
        if (folders.Count == 0)
        {
            Status = $"{_code.Root} has no folders to put a flow node in.";
            return;
        }

        string selected = SelectedFolder?.Path ?? "";
        string folder;
        if (_code.Holds(selected) && selected != AssetCatalog.CodePrefix)
        {
            folder = _code.GetLocalPath(selected);
        }
        else
        {
            AssetCatalog catalog = FindCatalog(selected);
            string local = catalog.GetLocalPath(selected);
            folder = SuggestCodeFolder(folders, local);
        }

        Status = "";
        Prompt = new FlowNodePrompt(folders, folder);
    }

    /// <summary>Writes the prompted flow node class and shows it; a class that can't be written keeps the prompt and says why.</summary>
    [RelayCommand]
    public void ConfirmPrompt()
    {
        if (Prompt is not { } prompt || _code is null)
        {
            return;
        }

        string codePath = Combine(AssetCatalog.CodePrefix, prompt.Folder);
        string fullFolder = _code.GetFullPath(codePath);
        string file;
        try
        {
            file = FlowNodeSource.Create(fullFolder, prompt.Name);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Status = exception.Message;
            return;
        }

        Prompt = null;
        string fileName = Path.GetFileName(file);
        string path = Combine(codePath, fileName);
        SearchText = "";
        Refresh();
        SelectFolder(codePath);
        SelectedAsset = Assets.FirstOrDefault(entry => entry.Path == path);
        Status = $"Wrote {fileName}. Build the project to add the node to flow graphs.";
    }

    [RelayCommand]
    public void CancelPrompt()
    {
        Prompt = null;
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
        if (recursive || SelectedFolder is null)
        {
            Items = Assets;
            return;
        }

        List<AssetEntry> items = [];
        foreach (AssetFolder child in SelectedFolder.Children)
        {
            items.Add(new AssetEntry(child.Path, child.Name, AssetKind.Folder));
        }

        items.AddRange(Assets);
        Items = items;
    }

    private void CreateFile(string name, string extension, string schema, string? body)
    {
        Status = "";
        string folder = SelectedFolder?.Path ?? "";
        if (RefuseCode(folder))
        {
            return;
        }

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

    /// <summary>Refuses an operation on code, which the content manager doesn't hold; code is edited in the IDE.</summary>
    private bool RefuseCode(string? path)
    {
        if (path is null || !FindCatalog(path).IsCode)
        {
            return false;
        }

        Status = "Edit code in your IDE: the browser only adds flow nodes to the code.";
        return true;
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
        AssetEntry entry = new(path, "", AssetKind.Other);
        string fullPath = GetFullPath(entry);
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

    private static void CollectFolders(AssetFolder folder, List<string> paths)
    {
        foreach (AssetFolder child in folder.Children)
        {
            string local = child.Path[(AssetCatalog.CodePrefix.Length + 1)..];
            paths.Add(local);
            CollectFolders(child, paths);
        }
    }

    /// <summary>The code folder sharing the most trailing folder names with the content folder; with none, the first holding a C# project.</summary>
    private string SuggestCodeFolder(List<string> folders, string contentFolder)
    {
        string[] content = contentFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? best = null;
        int bestScore = 0;
        foreach (string folder in folders)
        {
            string[] code = folder.Split('/');
            int score = 0;
            while (score < content.Length && score < code.Length
                && string.Equals(content[^(score + 1)], code[^(score + 1)], StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }

            if (score > bestScore)
            {
                best = folder;
                bestScore = score;
            }
        }

        if (best is not null)
        {
            return best;
        }

        foreach (string folder in folders)
        {
            string codePath = Combine(AssetCatalog.CodePrefix, folder);
            string fullFolder = _code!.GetFullPath(codePath);
            bool hasProject = Directory.EnumerateFiles(fullFolder, "*.csproj").Any();
            if (hasProject)
            {
                return folder;
            }
        }

        return folders[0];
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
