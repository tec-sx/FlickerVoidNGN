using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// An item of the scene outline: an entity (<see cref="Entity"/>) or a folder (<see cref="IsFolder"/>, its full
/// <see cref="Path"/> such as <c>Lights/Hall</c>), with its icon named as in the shell's icon set.
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

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

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

    public override string ToString() => Name;
}
