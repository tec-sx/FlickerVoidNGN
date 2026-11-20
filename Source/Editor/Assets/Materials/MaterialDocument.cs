using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>
/// A material file being edited: each change is held unsaved in the content, so everything that draws the material
/// picks it up, until it is saved or discarded.
/// </summary>
public sealed class MaterialDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly UnsavedContent _content;

    public MaterialDocument(UnsavedContent content, string path)
    {
        _content = content;
        Path = path;
        _content.Unload(path);
        MaterialFile loaded = content.Load(path, RenderingJsonContext.Default.MaterialFile);
        Material = new MaterialFile { Parameters = new Dictionary<string, JsonElement>(loaded.Parameters) };
    }

    public string Path { get; }

    /// <summary>The values as edited; a parameter it leaves out keeps the shader's default.</summary>
    public MaterialFile Material { get; }

    public event Action? Changed;

    public void Set(string parameter, JsonElement value)
    {
        Material.Parameters[parameter] = value;
        Hold();
        Changed?.Invoke();
    }

    private void Hold()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, Material, RenderingJsonContext.Default.MaterialFile);
        }

        byte[] contents = stream.ToArray();
        _content.Edit(Path, contents);
    }
}
