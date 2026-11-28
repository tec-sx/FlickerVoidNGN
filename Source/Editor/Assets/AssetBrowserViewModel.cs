using System.Diagnostics.CodeAnalysis;
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
/// The asset browser: the content folder's tree, the files of the selected folder, and a type filter and search that look
/// through everything below it. Opening a file raises <see cref="Opened"/>, which the editor answers by type. It creates,
/// renames and deletes files and folders through the content manager, so their source files follow in a development build.
/// Given the project's code folder, it shows it as a second root, Code, where it only writes new flow node classes.
/// </summary>
public sealed partial class AssetBrowserViewModel : ObservableObject
{
    private const string SchemaFolder = ".schemas/";
    private const string FlowGroup = "Flow";
    private const string DataGroup = "Data Asset";
    private const string TableGroup = "Table";

    private readonly ImportSources _imports;
    private readonly IContentManager _content;
    private readonly List<AssetCatalog> _catalogs;
    private readonly AssetCatalog? _code;
    private readonly bool _hasCode;
    private readonly HashSet<string> _expanded = [];

    /// <summary>
    /// Browses the content; <paramref name="data"/> lists the data asset and table types it can create, and
    /// <paramref name="codeFolder"/> is the project's source folder, where flow node classes go.
    /// </summary>
    private DataTypes? _data;

    public AssetBrowserViewModel(IContentManager content, DataTypes? data = null, string? codeFolder = null)
    {
        _content = content;
        _imports = new ImportSources(content);
        _hasCode = codeFolder is not null;
        _data = data;
        ListDataTypes(data);
        _catalogs = AssetCatalog.ForContent(content);
        if (codeFolder is not null)
        {
            _code = AssetCatalog.ForCode(codeFolder);
            _catalogs.Add(_code);
        }

        Filters = AssetTypes.Filters;
        SelectedFilter = Filters[0];
        Refresh();
    }

    /// <summary>
    /// Lists what <see cref="NewChoices"/> creates: a folder, each asset type New makes, and the data asset and table types
    /// <paramref name="data"/> registers, as again after the project's code is reloaded.
    /// </summary>
    [MemberNotNull(nameof(NewChoices))]
    public void ListDataTypes(DataTypes? data)
    {
        _data = data;
        List<MenuChoice> choices = [new("Folder", CreateFolder, Icon: "NewFolder")];
        foreach (AssetType type in AssetTypes.Files)
        {
            if (type.New is { } made)
            {
                choices.Add(new MenuChoice(made.Label, () => Create(type), made.Group, type.Icon));
            }
        }
        if (_hasCode)
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
    }

    /// <summary>Raised when a file is opened, by double-click or Enter.</summary>
    public event Action<AssetEntry>? Opened;

    public IReadOnlyList<AssetFilter> Filters { get; }

    /// <summary>
    /// What the browser can create in the selected folder, in groups: folders and scenes, then materials and moods, flows, a data
    /// asset of each registered type and a table of each row type. Each menu showing them picks how to group them.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<MenuChoice> NewChoices { get; private set; }

    /// <summary>The roots of the tree: the game's source folders in a development build, else the content folder.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetFolder> Folders { get; set; } = [];

    [ObservableProperty]
    public partial AssetFolder? SelectedFolder { get; set; }

    [ObservableProperty]
    public partial AssetFilter SelectedFilter { get; set; }

    /// <summary>The breadcrumb's start: the folders above the selected one, each followed by a slash (Content / Art / ).</summary>
    public string FolderTrail
    {
        get
        {
            if (SelectedFolder is null || Folders.Count == 0)
            {
                return "";
            }

            List<string> names = [];
            CollectTrail(Folders, SelectedFolder, names);
            return string.Concat(names.Select(static name => $"{name} / "));
        }
    }

    /// <summary>The breadcrumb's end: the selected folder's name.</summary>
    public string FolderName => SelectedFolder?.Name ?? "";

    /// <summary>What the search box says it searches.</summary>
    public string SearchHint => $"Search in {FolderName}";

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<AssetEntry> Assets { get; set; } = [];

    /// <summary>What the browser shows: the selected folder's subfolders, then <see cref="Assets"/>; only the files while searching or filtering.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AssetEntry> Items { get; set; } = [];

    /// <summary>The file or folder item renaming, deleting and moving work on; with none, renaming works on the selected folder.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReimport))]
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

    /// <summary>Every file of every catalog, for searches outside the browser (the command palette).</summary>
    public IReadOnlyList<AssetEntry> FindFiles()
    {
        List<AssetEntry> files = [];
        foreach (AssetCatalog catalog in _catalogs)
        {
            IReadOnlyList<AssetEntry> found = catalog.Find("", recursive: true, type: null, "");
            files.AddRange(found.Where(entry => entry.Type != AssetTypes.Folder));
        }

        return files;
    }

    [RelayCommand]
    public void Open(AssetEntry? asset)
    {
        if (asset is null)
        {
            return;
        }

        if (asset.Type == AssetTypes.Folder)
        {
            SelectFolder(asset.Path);
            return;
        }

        Opened?.Invoke(asset);
    }

    /// <summary>Shows the file's folder with the file selected, as Unreal's Browse to Asset does; null when there is no such file.</summary>
    public AssetEntry? Reveal(string path)
    {
        string folder = GetFolder(path);
        SelectFolder(folder);
        AssetEntry? owner = Assets.FirstOrDefault(asset => asset.Owned.Any(owned => owned.Path == path));
        if (owner is not null && _expanded.Add(owner.Path))
        {
            FindAssets();
        }

        AssetEntry? found = Items.FirstOrDefault(item => item.Path == path);
        if (found is not null)
        {
            SelectedAsset = found;
        }

        return found;
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

    /// <summary>Lists a group's owned files after it, or folds them away again.</summary>
    [RelayCommand]
    public void ToggleExpanded(AssetEntry entry)
    {
        if (!_expanded.Remove(entry.Path))
        {
            _expanded.Add(entry.Path);
        }

        FindAssets();
    }

    /// <summary>
    /// Moves a file or folder into a folder, keeping its name, and a file of a group with the whole group; a folder never goes
    /// into itself or below it.
    /// </summary>
    public void MoveTo(AssetEntry item, string folder)
    {
        Status = "";
        if (RefuseCode(item.Path) || RefuseCode(folder))
        {
            return;
        }

        bool intoItself = folder == item.Path || folder.StartsWith(item.Path + "/", StringComparison.Ordinal);
        if (item.Type == AssetTypes.Folder && intoItself)
        {
            Status = $"{item.Name} can't go into itself.";
            return;
        }

        AssetEntry group = GetGroup(item);
        if (GetFolder(group.Path) == folder)
        {
            return;
        }

        if (MoveGroup(group, folder, group.Name, copy: false) is not null)
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

    /// <summary>Creates a file of the type, as its <see cref="AssetType.New"/> writes one, in the selected folder and selects it.</summary>
    public void Create(AssetType type)
    {
        AssetNew made = type.New ?? throw new ArgumentException($"New makes no {type.Name} files.", nameof(type));
        CreateFile(made.FileName, type.Extensions[0], made.Schema, made.Write);
    }

    /// <summary>Creates a data asset of the type registered under <paramref name="type"/>, with its default values.</summary>
    public void CreateData(string type)
    {
        string name = DisplayNames.FromIdentifier(type);
        Func<string, string> write = AssetNew.Json($"\"type\": \"{type}\"");
        string extension = DataAssets.AssetExtension;
        if (_data is not null && _data.AssetExtensions.TryGetValue(type, out string? own))
        {
            extension = own;
        }

        CreateFile($"New {name}", extension, "data", write);
    }

    /// <summary>Creates an empty data table of the row type registered under <paramref name="type"/>.</summary>
    public void CreateTable(string type)
    {
        string name = DisplayNames.FromIdentifier(type);
        Func<string, string> write = AssetNew.Json($"\"type\": \"{type}\",\n  \"rows\": {{}}");
        CreateFile($"New {name} Table", DataAssets.TableExtension, "table", write);
    }

    /// <summary>
    /// Renames the selected file, keeping its extensions, with the other files of its group, or the selected folder when no file
    /// is selected.
    /// </summary>
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

        if (SelectedAsset is { } selected)
        {
            AssetEntry asset = GetGroup(selected);
            string assetFolder = GetFolder(asset.Path);
            if (MoveGroup(asset, assetFolder, name, copy: false) is { } newPath)
            {
                if (asset.Type == AssetTypes.Folder)
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

    /// <summary>
    /// Deletes the selected file with the files it owns (an owned file goes alone), or the selected folder with everything in it
    /// when no file is selected.
    /// </summary>
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
            List<AssetEntry> files = [asset, .. asset.Owned];
            foreach (AssetEntry file in files)
            {
                _content.Delete(file.Path);
                _imports.Remove(file.Path);
            }

            SelectedAsset = null;
            if (asset.Type == AssetTypes.Folder)
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
            _imports.Remove(folder.Path);
            Refresh();
            SelectFolder(parent);
        }
    }

    /// <summary>Copies the selected file with the other files of its group under the first free numbered name, and selects the copy.</summary>
    [RelayCommand]
    public void Duplicate()
    {
        Status = "";
        if (SelectedAsset is not { } selected || RefuseCode(selected.Path))
        {
            return;
        }

        if (selected.Type == AssetTypes.Folder)
        {
            Status = "Only files are duplicated.";
            return;
        }

        AssetEntry asset = GetGroup(selected);
        string folder = GetFolder(asset.Path);
        string name = FindFreeName(asset, folder);
        string? copy = MoveGroup(asset, folder, name, copy: true);
        FindAssets();
        SelectedAsset = Items.FirstOrDefault(entry => entry.Path == copy);
    }

    /// <summary>Whether the selected file was imported from outside the content folder, so it can be imported again.</summary>
    public bool CanReimport => SelectedAsset is { } asset && _imports.Find(asset.Path) is not null;

    /// <summary>
    /// Copies files from outside the content folder into the selected folder and remembers where they came from; a file
    /// imported again from the same place replaces its copy, and another file with a taken name gets a free one.
    /// </summary>
    public void Import(IReadOnlyList<string> fullPaths) => Import(fullPaths, SelectedFolder?.Path ?? "");

    /// <summary>Copies files from outside the content folder into the given folder, as <see cref="Import(IReadOnlyList{string})"/>.</summary>
    public void Import(IReadOnlyList<string> fullPaths, string folder)
    {
        Status = "";
        if (RefuseCode(folder))
        {
            return;
        }

        string? imported = null;
        foreach (string fullPath in fullPaths)
        {
            string fileName = Path.GetFileName(fullPath);
            string path = Combine(folder, fileName);
            string? source = _imports.Find(path);
            bool sameSource = source is not null && ContentManager.PathComparer.Equals(source, fullPath);
            if (Exists(path) && !sameSource)
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                path = FindFreePath(folder, name, extension);
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            _content.Write(path, bytes);
            _imports.Set(path, fullPath);
            imported = path;
        }

        SearchText = "";
        FindAssets();
        SelectedAsset = Assets.FirstOrDefault(entry => entry.Path == imported);
    }

    /// <summary>Copies the selected file again from where it was imported, after it changed there.</summary>
    [RelayCommand]
    public void Reimport()
    {
        Status = "";
        if (SelectedAsset is not { } asset || _imports.Find(asset.Path) is not { } source)
        {
            return;
        }

        if (!File.Exists(source))
        {
            Status = $"{asset.Name} can't be imported again: {source} is gone.";
            return;
        }

        byte[] bytes = File.ReadAllBytes(source);
        _content.Write(asset.Path, bytes);
        Status = $"Imported {asset.Name} again from {source}.";
    }

    /// <summary>The full path of a content file's source, which is the content folder's own file without a build.</summary>
    public string GetSourcePath(string path)
    {
        AssetCatalog catalog = FindCatalog(path);
        return catalog.GetFullPath(path);
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

    partial void OnSelectedFolderChanged(AssetFolder? value)
    {
        OnPropertyChanged(nameof(FolderTrail));
        OnPropertyChanged(nameof(FolderName));
        OnPropertyChanged(nameof(SearchHint));
        FindAssets();
    }

    partial void OnSelectedFilterChanged(AssetFilter value) => FindAssets();

    partial void OnSearchTextChanged(string value) => FindAssets();

    /// <summary>Fills <paramref name="names"/> with the folders from a root down to the one above <paramref name="wanted"/>.</summary>
    private static bool CollectTrail(IReadOnlyList<AssetFolder> folders, AssetFolder wanted, List<string> names)
    {
        foreach (AssetFolder folder in folders)
        {
            if (folder == wanted)
            {
                return true;
            }

            names.Add(folder.Name);
            if (CollectTrail(folder.Children, wanted, names))
            {
                return true;
            }

            names.RemoveAt(names.Count - 1);
        }

        return false;
    }

    private void FindAssets()
    {
        if (Folders.Count == 0)
        {
            return;
        }

        string folder = SelectedFolder?.Path ?? Folders[0].Path;
        IReadOnlyList<AssetType> types = SelectedFilter?.Types ?? [];
        string text = SearchText ?? "";
        bool recursive = types.Count > 0 || text.Length > 0;
        AssetCatalog catalog = FindCatalog(folder);
        Assets = catalog.Find(folder, recursive, types, text);
        List<AssetEntry> items = [];
        if (!recursive && SelectedFolder is not null)
        {
            foreach (AssetFolder child in SelectedFolder.Children)
            {
                items.Add(new AssetEntry(child.Path, child.Name, AssetTypes.Folder));
            }
        }

        foreach (AssetEntry asset in Assets)
        {
            if (asset.Owned.Count == 0 || !_expanded.Contains(asset.Path))
            {
                items.Add(asset);
                continue;
            }

            items.Add(asset with { IsExpanded = true });
            items.AddRange(asset.Owned);
        }

        Items = items;
    }

    private void CreateFile(string name, string extension, string schema, Func<string, string> write)
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
        string contents = write(schemaPath);
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
        return FindFreePath(folder, name, extension);
    }

    private string FindFreePath(string folder, string name, string extension)
    {
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
        _imports.Move(path, newPath);
        return true;
    }

    /// <summary>The group a file is part of: its owner with the owned files, or the file itself when no file owns it.</summary>
    private AssetEntry GetGroup(AssetEntry entry)
    {
        if (entry.Owner is not { } owner)
        {
            return entry;
        }

        string folder = GetFolder(owner);
        AssetCatalog catalog = FindCatalog(folder);
        IReadOnlyList<AssetEntry> entries = catalog.Find(folder, recursive: false, type: null, text: "");
        return entries.FirstOrDefault(found => found.Path == owner) ?? entry;
    }

    /// <summary>
    /// Moves or copies a group to the name in the folder, each file keeping what follows the group's name (Van.blend goes as
    /// Truck.blend with Van.fvvehicle as Truck.fvvehicle); refused, with nothing moved, when any new path is taken. Returns the
    /// group's new path.
    /// </summary>
    private string? MoveGroup(AssetEntry group, string folder, string name, bool copy)
    {
        List<AssetEntry> files = [group, .. group.Owned];
        List<string> newPaths = [];
        foreach (AssetEntry file in files)
        {
            string newPath = GetGroupPath(file, group, folder, name);
            if (Exists(newPath))
            {
                Status = $"{newPath} already exists.";
                return null;
            }

            newPaths.Add(newPath);
        }

        for (int index = 0; index < files.Count; index++)
        {
            string path = files[index].Path;
            if (copy)
            {
                string sourcePath = GetSourcePath(path);
                byte[] bytes = File.ReadAllBytes(sourcePath);
                _content.Write(newPaths[index], bytes);
            }
            else
            {
                _content.Move(path, newPaths[index]);
                _imports.Move(path, newPaths[index]);
            }
        }

        if (!copy && _expanded.Remove(group.Path))
        {
            _expanded.Add(newPaths[0]);
        }

        return newPaths[0];
    }

    /// <summary>The group's name numbered from 2, the first that none of its files takes in the folder.</summary>
    private string FindFreeName(AssetEntry group, string folder)
    {
        List<AssetEntry> files = [group, .. group.Owned];
        int number = 2;
        while (true)
        {
            string name = $"{group.Name} {number}";
            bool taken = false;
            foreach (AssetEntry file in files)
            {
                string path = GetGroupPath(file, group, folder, name);
                taken |= Exists(path);
            }

            if (!taken)
            {
                return name;
            }

            number++;
        }
    }

    /// <summary>The path a file of the group has once the group is named <paramref name="name"/> in the folder.</summary>
    private static string GetGroupPath(AssetEntry file, AssetEntry group, string folder, string name)
    {
        string fileName = file.Path[(file.Path.LastIndexOf('/') + 1)..];
        string rest = fileName[group.Name.Length..];
        return Combine(folder, name + rest);
    }

    /// <summary>Whether the path is taken in the content or in its source, which holds files the build leaves out (Van.blend).</summary>
    private bool Exists(string path)
    {
        AssetEntry entry = new(path, "", AssetTypes.Other);
        string fullPath = GetFullPath(entry);
        string sourcePath = GetSourcePath(path);
        return File.Exists(fullPath) || Directory.Exists(fullPath) || File.Exists(sourcePath) || Directory.Exists(sourcePath);
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
