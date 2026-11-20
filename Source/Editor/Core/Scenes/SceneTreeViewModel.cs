using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>One entity of the open scene in the scene list.</summary>
public sealed record SceneTreeItem(Entity Entity, string Name);

/// <summary>The open scene's entities, in the file's order; choosing one selects it, and selecting one elsewhere highlights it.</summary>
public sealed partial class SceneTreeViewModel : ObservableObject
{
    private readonly EditorScene _scene;
    private bool _syncing;

    public SceneTreeViewModel(EditorScene scene)
    {
        _scene = scene;
        _scene.EntitiesChanged += Rebuild;
        _scene.Selection.Changed += ShowSelection;
        Rebuild();
    }

    [ObservableProperty]
    public partial IReadOnlyList<SceneTreeItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial SceneTreeItem? SelectedItem { get; set; }

    /// <summary>Picks up renames made in the inspector.</summary>
    public void Refresh()
    {
        World world = _scene.Game.World;
        foreach (SceneTreeItem item in Items)
        {
            if (!world.IsAlive(item.Entity) || NameOf(world, item.Entity) != item.Name)
            {
                Rebuild();
                return;
            }
        }
    }

    partial void OnSelectedItemChanged(SceneTreeItem? value)
    {
        if (!_syncing)
        {
            _scene.Selection.Select(value?.Entity);
        }
    }

    private static string NameOf(World world, Entity entity)
    {
        return world.TryGet(entity, out EntityName name) ? name.Value : $"Entity {entity.Id}";
    }

    private void Rebuild()
    {
        World world = _scene.Game.World;
        Items = [.. _scene.Entities.Select(entity => new SceneTreeItem(entity, NameOf(world, entity)))];
        ShowSelection();
    }

    private void ShowSelection()
    {
        _syncing = true;
        try
        {
            Entity? selected = _scene.Selection.Selected;
            SelectedItem = Items.FirstOrDefault(item => item.Entity == selected);
        }
        finally
        {
            _syncing = false;
        }
    }
}
