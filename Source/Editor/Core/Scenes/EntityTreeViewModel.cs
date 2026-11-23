using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// The Entity tab's outline: the whole tree of the selected entity, from the entity at its top down through everything nested under
/// it, all shown. Choosing items selects their entities, and a selection made elsewhere shows as <see cref="SelectedItems"/>.
/// </summary>
public sealed partial class EntityTreeViewModel : ObservableObject
{
    private readonly EditorScene _scene;
    private Entity? _root;
    private bool _choosing;

    public EntityTreeViewModel(EditorScene scene)
    {
        _scene = scene;
        _scene.EntitiesChanged += Rebuild;
        _scene.Selection.Changed += HandleSelectionChanged;
        _scene.VisibilityChanged += ShowHidden;
        Rebuild();
    }

    [ObservableProperty]
    public partial IReadOnlyList<SceneTreeItem> Items { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SceneTreeItem> SelectedItems { get; private set; } = [];

    /// <summary>The name of the entity at the top of the tree, or empty with nothing selected.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    public void Choose(IReadOnlyList<SceneTreeItem> items)
    {
        List<Entity> entities = [.. items.SelectMany(item => item.GetEntities())];
        Entity? primary = entities.Count > 0 ? entities[^1] : null;
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

    /// <summary>Nests the entities under <paramref name="parent"/>, or takes them off their parents for null.</summary>
    public void Nest(IEnumerable<Entity> entities, Entity? parent)
    {
        _scene.SetParent(entities, parent);
    }

    private void HandleSelectionChanged()
    {
        if (_choosing)
        {
            return;
        }

        if (FindRoot() != _root)
        {
            Rebuild();
            return;
        }

        ShowSelection();
    }

    /// <summary>The entity at the top of the selection's tree; a loop of parents stops where it comes back round.</summary>
    private Entity? FindRoot()
    {
        if (_scene.Selection.Selected is not { } selected || !_scene.Game.World.IsAlive(selected))
        {
            return null;
        }

        Entity root = selected;
        HashSet<Entity> seen = [root];
        while (_scene.GetParent(root) is { } parent && seen.Add(parent))
        {
            root = parent;
        }

        return root;
    }

    private void Rebuild()
    {
        _root = FindRoot();
        if (_root is not { } root)
        {
            Items = [];
            Title = "";
            SelectedItems = [];
            return;
        }

        World world = _scene.Game.World;
        HashSet<Entity> seen = [root];
        SceneTreeItem top = CreateItem(world, root, seen);
        Items = [top];
        Title = top.Name;
        SceneTreeItem.ShowHidden(_scene, Items);
        ShowSelection();
    }

    private SceneTreeItem CreateItem(World world, Entity entity, HashSet<Entity> seen)
    {
        List<SceneTreeItem> children = [];
        foreach (Entity child in _scene.GetChildren(entity))
        {
            if (seen.Add(child))
            {
                children.Add(CreateItem(world, child, seen));
            }
        }

        string name = world.TryGet(entity, out EntityName entityName) ? entityName.Value : $"Entity {entity.Id}";
        string icon = SceneIcons.For(world, entity, _scene.Game.Spawner.Components);
        return new SceneTreeItem(name, "", icon, entity, children) { HideToggled = ToggleHidden };
    }

    private void ToggleHidden(SceneTreeItem item) => SceneTreeItem.ToggleHidden(_scene, item);

    private void ShowHidden() => SceneTreeItem.ShowHidden(_scene, Items);

    private void ShowSelection()
    {
        HashSet<Entity> selected = [.. _scene.Selection.Entities];
        SelectedItems = [.. SceneTreeItem.Flatten(Items).Where(item => item.Entity is { } entity && selected.Contains(entity))];
    }
}
