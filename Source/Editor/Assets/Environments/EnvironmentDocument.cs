using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Environments;

/// <summary>
/// An environment file being edited: the environment editor edits <see cref="Environment"/> in place, and
/// <see cref="ContentDocument{T}.Hold"/> keeps it unsaved in the content until it is saved or discarded.
/// </summary>
public sealed class EnvironmentDocument(UnsavedContent content, string path)
    : ContentDocument<LightingEnvironment>(content, path, RenderingJsonContext.Default.LightingEnvironment)
{
    public LightingEnvironment Environment => Value;
}
