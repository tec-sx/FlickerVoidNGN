using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// The open scene's outline: folders (from each entity's <see cref="EntityFolder"/>, plus new empty ones until something goes in),
/// subfolders before entities, entities in the file's order. Choosing items selects their entities, a folder all of its own;
/// a selection made elsewhere shows as <see cref="SelectedItems"/>. Folders are created, renamed and deleted here.
/// </summary>
public sealed partial class SceneTreeViewModel : ObservableObject
{
    private const char Separator = '/';

    private readonly EditorScene _scene;
    private readonly HashSet<string> _emptyFolders = [];
    private readonly HashSet<string> _collapsed = [];
    private IReadOnlyList<SceneTreeItem> _chosenFolders = [];
    private bool _choosing;

    public SceneTreeViewModel(EditorScene scene)
    {
        _scene = scene;
        _scene.EntitiesChanged += Rebuild;
        _scene.Opened += HandleOpened;
        _scene.Selection.Changed += ShowSelection;
        Rebuild();
    }

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

    /// <summary>Files the entities under the folder, or at the top for null or empty.</summary>
    public void MoveTo(IEnumerable<Entity> entities, string? folder)
    {
        foreach (Entity entity in entities.ToList())
        {
            _scene.SetFolder(entity, folder);
        }

        Rebuild();
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
        foreach (SceneTreeItem item in Flatten(Items))
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
        foreach (SceneTreeItem item in Flatten(Items))
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
            if (FolderOf(world, entity) != parent)
            {
                continue;
            }

            string icon = SceneIcons.For(world, entity, _scene.Game.Spawner.Components);
            items.Add(new SceneTreeItem(NameOf(world, entity), parent, icon, entity, []));
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
        foreach (SceneTreeItem item in Flatten(Items))
        {
            if (item.Entity is { } entity && selected.Contains(entity))
            {
                shown.Add(item);
            }
        }

        foreach (SceneTreeItem folder in _chosenFolders)
        {
            SceneTreeItem? current = Flatten(Items).FirstOrDefault(item => item.IsFolder && item.Path == folder.Path);
            if (current is not null && current.GetEntities().All(selected.Contains))
            {
                shown.Add(current);
            }
        }

        SelectedItems = shown;
    }

    private static IEnumerable<SceneTreeItem> Flatten(IEnumerable<SceneTreeItem> items)
    {
        foreach (SceneTreeItem item in items)
        {
            yield return item;
            foreach (SceneTreeItem child in Flatten(item.Children))
            {
                yield return child;
            }
        }
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
