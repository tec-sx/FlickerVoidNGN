using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// An item of the scene outline: an entity (<see cref="Entity"/>) or a folder (<see cref="IsFolder"/>, its full
/// <see cref="Path"/> such as <c>Lights/Hall</c>), with its icon named as in the shell's icon set. An entity with nested entities
/// (<see cref="HasNested"/>) shows them as children once revealed, each marked <see cref="IsNested"/>; the row's eye hides it in the
/// editor's views.
/// </summary>
public sealed partial class SceneTreeItem(string name, string path, string icon, Entity? entity, IReadOnlyList<SceneTreeItem> children)
    : ObservableObject
{
    public string Name { get; } = name;

    /// <summary>A folder's full path, or the folder an entity is filed under ("" at the top).</summary>
    public string Path { get; } = path;

    public string Icon { get; } = icon;

    public Entity? Entity { get; } = entity;

    public IReadOnlyList<SceneTreeItem> Children { get; } = children;

    public bool IsFolder => Entity is null;

    /// <summary>How many items it holds, said beside its name; nothing is said for none.</summary>
    public int Count => Children.Count;

    public bool HasCount => Children.Count > 0;

    /// <summary>Whether the entity is shown under the entity it is nested under.</summary>
    public bool IsNested { get; init; }

    /// <summary>Whether entities are nested under this one, so the row offers to reveal them.</summary>
    public bool HasNested { get; init; }

    /// <summary>What the row's buttons do: hide or show the item, and reveal or conceal what is nested under it.</summary>
    public Action<SceneTreeItem>? HideToggled { get; init; }

    public Action<SceneTreeItem>? RevealToggled { get; init; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>Whether the entities nested under this one are shown under it.</summary>
    [ObservableProperty]
    public partial bool IsRevealed { get; set; }

    /// <summary>Whether the item, or everything in a folder, is hidden in the editor's views.</summary>
    [ObservableProperty]
    public partial bool IsHidden { get; set; }

    /// <summary>This item's entity, or every entity in the folder and its subfolders.</summary>
    public IEnumerable<Entity> GetEntities()
    {
        if (Entity is { } entity)
        {
            yield return entity;
            yield break;
        }

        foreach (SceneTreeItem child in Children)
        {
            foreach (Entity inner in child.GetEntities())
            {
                yield return inner;
            }
        }
    }

    /// <summary>The items and everything under them, parents first.</summary>
    public static IEnumerable<SceneTreeItem> Flatten(IEnumerable<SceneTreeItem> items)
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

    /// <summary>Marks each item hidden as the scene hides its entity, and a folder hidden when everything in it is.</summary>
    public static void ShowHidden(EditorScene scene, IEnumerable<SceneTreeItem> items)
    {
        foreach (SceneTreeItem item in Flatten(items))
        {
            List<Entity> entities = [.. item.GetEntities()];
            item.IsHidden = entities.Count > 0 && entities.All(scene.IsHidden);
        }
    }

    /// <summary>Hides the item's entities with everything nested under them, or shows them when they are hidden.</summary>
    public static void ToggleHidden(EditorScene scene, SceneTreeItem item)
    {
        IEnumerable<Entity> entities = item.GetEntities();
        scene.SetHidden(entities, !item.IsHidden);
    }

    [RelayCommand]
    private void ToggleHidden() => HideToggled?.Invoke(this);

    [RelayCommand]
    private void ToggleReveal() => RevealToggled?.Invoke(this);

    public override string ToString() => Name;
}
