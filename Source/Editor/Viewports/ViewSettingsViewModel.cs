using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>
/// The look of the viewport: a field for every setting of the lighting mood it draws the scene with, from the sun to the
/// tonemapper, generated from the mood's metadata and edited in place.
/// </summary>
public sealed class ViewSettingsViewModel
{
    public ViewSettingsViewModel(LightingMood mood)
    {
        Mood = mood;
        List<FieldViewModel> fields = [];
        foreach (JsonPropertyInfo info in RenderingJsonContext.Default.LightingMood.Properties)
        {
            if (info.Name == JsonSchemas.SchemaProperty || info.Get is null)
            {
                continue;
            }

            ObjectProperty property = new(mood, info);
            FieldViewModel field = FieldFactory.Create(property);
            fields.Add(field);
        }

        Fields = fields;
    }

    public LightingMood Mood { get; }

    public IReadOnlyList<FieldViewModel> Fields { get; }
}
