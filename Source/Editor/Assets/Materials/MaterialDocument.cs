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
    /// <summary>The values as edited; a parameter it leaves out keeps its parent's value, or the default without one.</summary>
    public MaterialFile Material => Value;

    public event Action? Changed;

    public void Set(string parameter, JsonElement value)
    {
        Material.Parameters[parameter] = value;
        Hold();
        Changed?.Invoke();
    }

    /// <summary>Takes the parameter's value out, so the material keeps its parent's or the default again.</summary>
    public void Remove(string parameter)
    {
        if (!Material.Parameters.Remove(parameter))
        {
            return;
        }

        Hold();
        Changed?.Invoke();
    }

    /// <summary>Makes the material an instance of another material or a master, or of none.</summary>
    public void SetParent(string? parent)
    {
        if (string.IsNullOrEmpty(parent))
        {
            parent = null;
        }

        if (Material.Parent == parent)
        {
            return;
        }

        Material.Parent = parent;
        Hold();
        Changed?.Invoke();
    }
}
