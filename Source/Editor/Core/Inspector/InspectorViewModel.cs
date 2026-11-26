
using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// The selected entity's components, with fields generated from the metadata each component registered for scene files,
/// so a new component needs no inspector code.
/// </summary>
public sealed partial class InspectorViewModel : ObservableObject
{
    private const string NothingSelected = "Nothing selected";

    private readonly EditorScene _scene;
    private readonly FindContentFiles _files;

    /// <param name="scene">The scene whose selection is inspected.</param>
    /// <param name="files">Finds the content files a field may name, to suggest them.</param>
    public InspectorViewModel(EditorScene scene, FindContentFiles files)
    {
        _scene = scene;
        _files = files;
        _scene.Selection.Changed += Rebuild;
        _scene.History.Applied += Rebuild;
        Rebuild();
    }

    /// <summary>The selected entity's name.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = NothingSelected;

    /// <summary>The entity template the selected entity starts from, picked from the content folder's; null when it isn't in the scene file.</summary>
    [ObservableProperty]
    public partial AssetPickerViewModel? Template { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ComponentViewModel> Components { get; set; } = [];

    /// <summary>The registered components the selected entity doesn't have yet.</summary>
    /// <summary>Text the components are filtered by: a component stays when its name or one of its fields' labels contains it.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    /// <summary>The components that match <see cref="FilterText"/>.</summary>
    public IReadOnlyList<ComponentViewModel> VisibleComponents => [.. Components.Where(MatchesFilter)];

    partial void OnFilterTextChanged(string value) => OnPropertyChanged(nameof(VisibleComponents));

    partial void OnComponentsChanged(IReadOnlyList<ComponentViewModel> value) => OnPropertyChanged(nameof(VisibleComponents));

    private bool MatchesFilter(ComponentViewModel component)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        if (component.Title.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return component.Fields.Any(field => field.Label.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    public partial IReadOnlyList<SceneComponent> AddableComponents { get; set; } = [];

    /// <summary>The addable components as the Add Component menu offers them, grouped by module with icons.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<MenuChoice> AddChoices { get; set; } = [];

    /// <summary>Gives the selected entity the component with its defaults.</summary>
    [RelayCommand]
    private void AddComponent(SceneComponent component)
    {
        if (_scene.Selection.Selected is not { } entity)
        {
            return;
        }

        _scene.AddComponent(entity, component);
        Rebuild();
    }

    [RelayCommand]
    private void RemoveComponent(ComponentViewModel shown)
    {
        if (!shown.CanRemove || _scene.Selection.Selected is not { } entity)
        {
            return;
        }

        _scene.RemoveComponent(entity, shown.Component);
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
            Template = null;
            Components = [];
            AddableComponents = [];
            AddChoices = [];
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

        Template = CreateTemplatePicker(entity);
        List<ComponentViewModel> components = [];
        List<SceneComponent> addable = [];
        foreach (SceneComponent component in _scene.Game.Spawner.Components)
        {
            if (component.Has(world, entity))
            {
                ComponentViewModel shown = new(_scene, entity, component, _files);
                components.Add(shown);
            }
            else
            {
                addable.Add(component);
            }
        }

        Components = components;
        AddableComponents = addable;
        AddChoices = ComponentChoices.Build(addable, AddComponent);
    }

    private AssetPickerViewModel? CreateTemplatePicker(Entity entity)
    {
        if (!_scene.Entities.Contains(entity))
        {
            return null;
        }

        AssetPickerViewModel picker = new(FindTemplates, acceptsNone: true, path => SetTemplate(entity, path));
        picker.Current = _scene.GetTemplate(entity);
        return picker;
    }

    private IReadOnlyList<string> FindTemplates() => _files([EntityTemplates.Extension]);

    private void SetTemplate(Entity entity, string? path)
    {
        _scene.SetTemplate(entity, path);
        Rebuild();
    }
}
