using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Moods;

/// <summary>
/// A lighting mood file being edited: the editor draws and edits <see cref="Mood"/> in place, and <see cref="ContentDocument{T}.Hold"/>
/// keeps its values unsaved in the content until they are saved or discarded.
/// </summary>
public sealed class MoodDocument(UnsavedContent content, string path)
    : ContentDocument<LightingMood>(content, path, RenderingJsonContext.Default.LightingMood)
{
    public LightingMood Mood => Value;
}
