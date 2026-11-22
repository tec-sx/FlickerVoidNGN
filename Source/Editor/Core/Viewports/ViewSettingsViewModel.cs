using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Editor.Core.Viewports;

/// <summary>
/// The look of the viewport: a field for every setting of the lighting mood it draws the scene with, from the sun to the
/// tonemapper, generated from the mood's metadata and edited in place, files such as the paper picked among those of their
/// kind; <see cref="Edited"/> follows each edit.
/// </summary>
public sealed class ViewSettingsViewModel
{
    public ViewSettingsViewModel(LightingMood mood, FindContentFiles? files = null)
    {
        Mood = mood;
        List<FieldViewModel> fields = [];
        foreach (JsonPropertyInfo info in RenderingJsonContext.Default.LightingMood.Properties)
        {
            if (info.Name == JsonSchemas.SchemaProperty || info.Get is null)
            {
                continue;
            }

            ObjectProperty property = new(mood, info, RaiseEdited);
            FieldViewModel field = FieldFactory.Create(property, files);
            fields.Add(field);
        }

        Fields = fields;
    }

    public event Action? Edited;

    public LightingMood Mood { get; }

    public IReadOnlyList<FieldViewModel> Fields { get; }

    private void RaiseEdited() => Edited?.Invoke();
}
