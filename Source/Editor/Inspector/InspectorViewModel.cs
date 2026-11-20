using System.Text.Json;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Editor.Assets;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>
/// The selected entity's components, with fields generated from the metadata each component registered for scene files,
/// so a new component needs no inspector code.
/// </summary>
public sealed partial class InspectorViewModel : ObservableObject
{
    private const string NothingSelected = "Nothing selected";

    private readonly EditorScene _scene;

    public InspectorViewModel(EditorScene scene)
    {
        _scene = scene;
        _scene.Selection.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>The selected entity's name.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = NothingSelected;

    [ObservableProperty]
    public partial IReadOnlyList<ComponentViewModel> Components { get; set; } = [];

    /// <summary>The registered components the selected entity doesn't have yet.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SceneComponent> AddableComponents { get; set; } = [];

    /// <summary>Gives the selected entity the component with its defaults.</summary>
    [RelayCommand]
    private void AddComponent(SceneComponent component)
    {
        World world = _scene.Game.World;
        if (_scene.Selection.Selected is not { } entity || !world.IsAlive(entity) || component.Has(world, entity))
        {
            return;
        }

        using JsonDocument defaults = JsonDocument.Parse("{}");
        component.Read(world, entity, defaults.RootElement);
        Rebuild();
    }

    [RelayCommand]
    private void RemoveComponent(ComponentViewModel shown)
    {
        World world = _scene.Game.World;
        if (!shown.CanRemove || _scene.Selection.Selected is not { } entity || !world.IsAlive(entity))
        {
            return;
        }

        shown.Component.Remove(world, entity);
        Rebuild();
    }

    /// <summary>Shows changes made elsewhere, such as by a gizmo, and clears when the entity is gone.</summary>
    public void Refresh()
    {
        World world = _scene.Game.World;
        if (_scene.Selection.Selected is not { } entity || !world.IsAlive(entity))
        {
            if (Components.Count > 0)
            {
                Rebuild();
            }

            return;
        }

        foreach (ComponentViewModel component in Components)
        {
            component.Refresh();
        }
    }

    private void Rebuild()
    {
        World world = _scene.Game.World;
        if (_scene.Selection.Selected is not { } entity || !world.IsAlive(entity))
        {
            Title = NothingSelected;
            Components = [];
            AddableComponents = [];
            return;
        }

        if (world.TryGet(entity, out EntityName name))
        {
            Title = name.Value;
        }
        else
        {
            Title = $"Entity {entity.Id}";
        }

        ContentFileFinder files = new(_scene.Content);
        List<ComponentViewModel> components = [];
        List<SceneComponent> addable = [];
        foreach (SceneComponent component in _scene.Game.Spawner.Components)
        {
            if (component.Has(world, entity))
            {
                ComponentViewModel shown = new(world, entity, component, files);
                components.Add(shown);
            }
            else
            {
                addable.Add(component);
            }
        }

        Components = components;
        AddableComponents = addable;
    }
}
