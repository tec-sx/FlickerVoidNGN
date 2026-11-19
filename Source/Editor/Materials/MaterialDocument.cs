using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Editor.Materials;

/// <summary>
/// A material file being edited: each change is saved at once and reported to the content manager, so everything that
/// draws the material picks it up.
/// </summary>
public sealed class MaterialDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly IContentManager _content;

    public MaterialDocument(IContentManager content, string path)
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
        Save();
        Changed?.Invoke();
    }

    private void Save()
    {
        string fullPath = _content.GetFullPath(Path);
        using (FileStream stream = File.Create(fullPath))
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, Material, RenderingJsonContext.Default.MaterialFile);
        }

        _content.NotifyChanged(Path);
    }
}
