using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>
/// A material file being edited: each change is held unsaved in the content, so everything that draws the material
/// picks it up, until it is saved or discarded.
/// </summary>
public sealed class MaterialDocument(UnsavedContent content, string path)
    : ContentDocument<MaterialFile>(content, path, RenderingJsonContext.Default.MaterialFile)
{
    /// <summary>The values as edited; a parameter it leaves out keeps the shader's default.</summary>
    public MaterialFile Material => Value;

    public event Action? Changed;

    public void Set(string parameter, JsonElement value)
    {
        Material.Parameters[parameter] = value;
        Hold();
        Changed?.Invoke();
    }
}
