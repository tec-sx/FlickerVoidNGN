using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

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
        foreach (SceneComponent component in _scene.Game.Spawner.Components)
        {
            if (component.Has(world, entity))
            {
                ComponentViewModel shown = new(world, entity, component, files);
                components.Add(shown);
            }
        }

        Components = components;
    }
}
