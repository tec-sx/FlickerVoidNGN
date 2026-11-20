using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>One component of the selected entity, with a field for each property its scene file metadata lists.</summary>
public sealed class ComponentViewModel
{
    public ComponentViewModel(EditorScene scene, Entity entity, SceneComponent component, FindContentFiles files)
    {
        Title = DisplayNames.FromIdentifier(component.TypeInfo.Type.Name);
        List<FieldViewModel> fields = [];
        foreach (JsonPropertyInfo info in component.TypeInfo.Properties)
        {
            if (info.Get is null)
            {
                continue;
            }

            ComponentProperty property = new(scene, entity, component, info);
            FieldViewModel field = FieldFactory.Create(property, files);
            fields.Add(field);
        }

        Fields = fields;
        Component = component;
    }

    public SceneComponent Component { get; }

    /// <summary>The transform stays: the viewport, picking and the gizmo need it.</summary>
    public bool CanRemove => Component.Name != "transform";

    public string Title { get; }

    public IReadOnlyList<FieldViewModel> Fields { get; }

    public void Refresh()
    {
        foreach (FieldViewModel field in Fields)
        {
            field.Refresh();
        }
    }
}
