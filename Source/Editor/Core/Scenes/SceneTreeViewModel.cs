using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// The open scene's outline: folders (from each entity's <see cref="EntityFolder"/>, plus new empty ones until something goes in),
/// subfolders before entities, entities in the file's order. Choosing items selects their entities, a folder all of its own;
/// a selection made elsewhere shows as <see cref="SelectedItems"/>. Folders are created, renamed and deleted here. Only entities at
/// the top are listed: what is nested under one shows under it, marked nested, while it is revealed (or all are,
/// <see cref="RevealAllNested"/>), and the Entity tab (<see cref="EntityTreeViewModel"/>) shows the whole tree of the selection.
/// </summary>
public sealed partial class SceneTreeViewModel : ObservableObject
{
    private const char Separator = '/';

    private readonly EditorScene _scene;
    private readonly HashSet<string> _emptyFolders = [];
    private readonly HashSet<string> _collapsed = [];
    private readonly HashSet<string> _revealed = [];
    private IReadOnlyList<SceneTreeItem> _chosenFolders = [];
    private bool _choosing;

    public SceneTreeViewModel(EditorScene scene)
    {
        _scene = scene;
        _scene.EntitiesChanged += Rebuild;
        _scene.Opened += HandleOpened;
        _scene.Selection.Changed += ShowSelection;
        _scene.VisibilityChanged += ShowHidden;
        Rebuild();
    }

    /// <summary>Whether every entity nested under another is shown under it.</summary>
    [ObservableProperty]
    public partial bool RevealAllNested { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<SceneTreeItem> Items { get; private set; } = [];

    /// <summary>The items whose entities are selected, and the folders chosen to select them.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SceneTreeItem> SelectedItems { get; private set; } = [];

    /// <summary>Selects the chosen items' entities: an entity item's own, every entity in a folder's.</summary>
    public void Choose(IReadOnlyList<SceneTreeItem> items)
    {
        List<Entity> entities = [];
        Entity? primary = null;
        foreach (SceneTreeItem item in items)
        {
            entities.AddRange(item.GetEntities());
            if (item.Entity is { } entity)
            {
                primary = entity;
            }
        }

        _chosenFolders = [.. items.Where(item => item.IsFolder)];
        _choosing = true;
        try
        {
            _scene.Selection.SelectAll(entities, primary);
        }
        finally
        {
            _choosing = false;
        }

        ShowSelection();
    }

    /// <summary>
    /// Creates a folder named "New Folder" (numbered when taken) in <paramref name="parent"/>, or at the top, and files the selected
    /// entities in it; returns its path.
    /// </summary>
    public string CreateFolder(string? parent)
    {
        string prefix = string.IsNullOrEmpty(parent) ? "" : parent + Separator;
        HashSet<string> taken = [.. GetFolderPaths()];
        string path = prefix + "New Folder";
        for (int number = 2; taken.Contains(path); number++)
        {
            path = $"{prefix}New Folder {number}";
        }

        _emptyFolders.Add(path);
        List<Entity> selected = [.. _scene.Selection.Entities];
        if (selected.Count > 0)
        {
            MoveTo(selected, path);
        }
        else
        {
            Rebuild();
        }

        return path;
    }

    /// <summary>Files the entities under the folder, or at the top for null or empty, taking them off their parents.</summary>
    public void MoveTo(IEnumerable<Entity> entities, string? folder)
    {
        _scene.History.BeginGroup("File in folder");
        foreach (Entity entity in entities.ToList())
        {
            _scene.SetParent(entity, null);
            _scene.SetFolder(entity, folder);
        }

        _scene.History.EndGroup();

        Rebuild();
    }

    /// <summary>Nests the entities under <paramref name="parent"/>, as dropping them on it does.</summary>
    public void Nest(IEnumerable<Entity> entities, Entity parent)
    {
        _scene.SetParent(entities, parent);
    }

    [RelayCommand]
    private void HideSelected() => _scene.SetHidden(_scene.Selection.Entities, hidden: true);

    [RelayCommand]
    private void ShowSelected() => _scene.SetHidden(_scene.Selection.Entities, hidden: false);

    [RelayCommand]
    private void ShowAll() => _scene.ShowAll();

    partial void OnRevealAllNestedChanged(bool value) => Rebuild();

    private void ToggleReveal(SceneTreeItem item)
    {
        if (!_revealed.Remove(item.Name))
        {
            _revealed.Add(item.Name);
        }

        Rebuild();
    }

    private void ToggleHidden(SceneTreeItem item) => SceneTreeItem.ToggleHidden(_scene, item);

    private void ShowHidden() => SceneTreeItem.ShowHidden(_scene, Items);

    /// <summary>Renames the item's entity, rewriting what names it; false for a folder, an empty name or one another entity has.</summary>
    public bool RenameEntity(SceneTreeItem item, string name)
    {
        if (item.Entity is not { } entity)
        {
            return false;
        }

        return _scene.RenameEntity(entity, name);
    }

    /// <summary>Renames the folder at <paramref name="path"/>, moving everything in it and its subfolders.</summary>
    public void RenameFolder(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains(Separator))
        {
            return;
        }

        int slash = path.LastIndexOf(Separator);
        string renamed = slash < 0 ? name : path[..(slash + 1)] + name;
        Refile(path, renamed);
    }

    /// <summary>Moves the folder at <paramref name="path"/> to <paramref name="moved"/>, such as into another folder.</summary>
    public void RenameFolderPath(string path, string moved)
    {
        if (moved.Length > 0 && moved != path)
        {
            Refile(path, moved);
        }
    }

    /// <summary>Removes the folder, filing what was in it in its parent.</summary>
    public void DeleteFolder(string path)
    {
        int slash = path.LastIndexOf(Separator);
        string parent = slash < 0 ? "" : path[..slash];
        Refile(path, parent);
    }

    /// <summary>Picks up renames made in the inspector.</summary>
    public void Refresh()
    {
        World world = _scene.Game.World;
        foreach (SceneTreeItem item in SceneTreeItem.Flatten(Items))
        {
            if (item.Entity is { } entity && (!world.IsAlive(entity) || NameOf(world, entity) != item.Name))
            {
                Rebuild();
                return;
            }
        }
    }

    /// <summary>Moves everything under <paramref name="from"/> to <paramref name="to"/>, subfolders keeping their place under it.</summary>
    private void Refile(string from, string to)
    {
        World world = _scene.Game.World;
        _scene.History.BeginGroup($"Refile {from}");
        foreach (Entity entity in _scene.Entities)
        {
            string folder = FolderOf(world, entity);
            if (folder == from || folder.StartsWith(from + Separator, StringComparison.Ordinal))
            {
                string rest = folder[from.Length..].TrimStart(Separator);
                string moved = Join(to, rest);
                _scene.SetFolder(entity, moved);
            }
        }

        _scene.History.EndGroup();

        foreach (string empty in _emptyFolders.ToList())
        {
            if (empty == from || empty.StartsWith(from + Separator, StringComparison.Ordinal))
            {
                _emptyFolders.Remove(empty);
                string rest = empty[from.Length..].TrimStart(Separator);
                string moved = Join(to, rest);
                if (moved.Length > 0)
                {
                    _emptyFolders.Add(moved);
                }
            }
        }

        Rebuild();
    }

    private static string Join(string first, string second)
    {
        if (first.Length == 0)
        {
            return second;
        }

        if (second.Length == 0)
        {
            return first;
        }

        return first + Separator + second;
    }

    private void HandleOpened()
    {
        _emptyFolders.Clear();
        _collapsed.Clear();
        _revealed.Clear();
        Rebuild();
    }

    private IEnumerable<string> GetFolderPaths()
    {
        World world = _scene.Game.World;
        HashSet<string> paths = [.. _emptyFolders];
        foreach (Entity entity in _scene.Entities)
        {
            string folder = FolderOf(world, entity);
            while (folder.Length > 0)
            {
                paths.Add(folder);
                int slash = folder.LastIndexOf(Separator);
                folder = slash < 0 ? "" : folder[..slash];
            }
        }

        return paths;
    }

    private void Rebuild()
    {
        foreach (SceneTreeItem item in SceneTreeItem.Flatten(Items))
        {
            if (item.IsFolder && !item.IsExpanded)
            {
                _collapsed.Add(item.Path);
            }
            else if (item.IsFolder)
            {
                _collapsed.Remove(item.Path);
            }
        }

        World world = _scene.Game.World;
        IReadOnlyList<Entity> entities = _scene.Entities;
        foreach (Entity entity in entities)
        {
            string folder = FolderOf(world, entity);
            _emptyFolders.RemoveWhere(empty => folder == empty || folder.StartsWith(empty + Separator, StringComparison.Ordinal));
        }

        HashSet<string> folders = [.. GetFolderPaths()];
        Items = BuildLevel("", folders, entities, world);
        ShowHidden();
        ShowSelection();
    }

    private List<SceneTreeItem> BuildLevel(string parent, HashSet<string> folders, IReadOnlyList<Entity> entities, World world)
    {
        List<SceneTreeItem> items = [];
        IEnumerable<string> children = folders.Where(path => ParentOf(path) == parent).Order(StringComparer.OrdinalIgnoreCase);
        foreach (string path in children)
        {
            List<SceneTreeItem> inner = BuildLevel(path, folders, entities, world);
            string name = path[(path.LastIndexOf(Separator) + 1)..];
            SceneTreeItem folder = new(name, path, SceneIcons.Folder, null, inner) { IsExpanded = !_collapsed.Contains(path) };
            items.Add(folder);
        }

        foreach (Entity entity in entities)
        {
            if (FolderOf(world, entity) != parent || _scene.GetParent(entity) is not null)
            {
                continue;
            }

            string name = NameOf(world, entity);
            bool revealed = RevealAllNested || _revealed.Contains(name);
            List<SceneTreeItem> nested = [];
            if (revealed)
            {
                HashSet<Entity> seen = [entity];
                nested = BuildNested(entity, parent, world, seen);
            }

            string icon = SceneIcons.For(world, entity, _scene.Game.Spawner.Components);
            bool hasNested = _scene.GetChildren(entity).Count > 0;
            SceneTreeItem item = new(name, parent, icon, entity, nested)
            {
                HasNested = hasNested,
                IsRevealed = revealed && hasNested,
                HideToggled = ToggleHidden,
                RevealToggled = ToggleReveal,
            };
            items.Add(item);
        }

        return items;
    }

    /// <summary>Everything nested under the entity, marked nested; <paramref name="seen"/> keeps a loop of parents from repeating.</summary>
    private List<SceneTreeItem> BuildNested(Entity entity, string folder, World world, HashSet<Entity> seen)
    {
        List<SceneTreeItem> items = [];
        foreach (Entity child in _scene.GetChildren(entity))
        {
            if (!seen.Add(child))
            {
                continue;
            }

            List<SceneTreeItem> inner = BuildNested(child, folder, world, seen);
            string icon = SceneIcons.For(world, child, _scene.Game.Spawner.Components);
            SceneTreeItem item = new(NameOf(world, child), folder, icon, child, inner) { IsNested = true, HideToggled = ToggleHidden };
            items.Add(item);
        }

        return items;
    }

    private void ShowSelection()
    {
        if (_choosing)
        {
            return;
        }

        HashSet<Entity> selected = [.. _scene.Selection.Entities];
        List<SceneTreeItem> shown = [];
        foreach (SceneTreeItem item in SceneTreeItem.Flatten(Items))
        {
            if (item.Entity is { } entity && selected.Contains(entity))
            {
                shown.Add(item);
            }
        }

        foreach (SceneTreeItem folder in _chosenFolders)
        {
            SceneTreeItem? current = SceneTreeItem.Flatten(Items).FirstOrDefault(item => item.IsFolder && item.Path == folder.Path);
            if (current is not null && current.GetEntities().All(selected.Contains))
            {
                shown.Add(current);
            }
        }

        SelectedItems = shown;
    }

    private static string ParentOf(string path)
    {
        int slash = path.LastIndexOf(Separator);
        return slash < 0 ? "" : path[..slash];
    }

    private static string FolderOf(World world, Entity entity)
    {
        return world.TryGet(entity, out EntityFolder folder) ? folder.Path : "";
    }

    private static string NameOf(World world, Entity entity)
    {
        return world.TryGet(entity, out EntityName name) ? name.Value : $"Entity {entity.Id}";
    }
}
