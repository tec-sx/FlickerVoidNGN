using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Engine.Editor.Assets;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>One component of the selected entity, with a field for each property its scene file metadata lists.</summary>
public sealed class ComponentViewModel
{
    public ComponentViewModel(World world, Entity entity, SceneComponent component, ContentFileFinder files)
    {
        Title = DisplayNames.FromIdentifier(component.TypeInfo.Type.Name);
        List<FieldViewModel> fields = [];
        foreach (JsonPropertyInfo info in component.TypeInfo.Properties)
        {
            if (info.Get is null)
            {
                continue;
            }

            ComponentProperty property = new(world, entity, component, info);
            FieldViewModel field = FieldFactory.Create(property, files);
            fields.Add(field);
        }

        Fields = fields;
    }

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
