using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>
/// A data asset file being edited: the editor edits <see cref="Asset"/> in place, and <see cref="Hold"/> keeps its values unsaved
/// in the content until they are saved or discarded.
/// </summary>
public sealed class DataDocument
{
    private readonly UnsavedContent _content;
    private readonly DataTypes _types;
    private readonly string? _schema;

    /// <summary>Reads the file; throws <see cref="InvalidDataException"/> when it holds no registered type or can't be read.</summary>
    public DataDocument(UnsavedContent content, DataTypes types, string path)
    {
        _content = content;
        _types = types;
        Path = path;

        // Unloading around the load reads the file's current contents, unsaved edits included, into objects nothing else holds.
        _content.Unload(path);
        JsonObject file = _content.Load(path, RuntimeJsonContext.Default.JsonObject);
        _content.Unload(path);
        _schema = file[JsonSchemas.SchemaProperty]?.GetValue<string>();
        Asset = types.ReadAsset(path, file);
    }

    public string Path { get; }

    public object Asset { get; }

    /// <summary>Keeps the asset's values as the file's unsaved contents.</summary>
    public void Hold()
    {
        byte[] contents = _types.WriteAsset(Asset, _schema);
        _content.Edit(Path, contents);
    }
}
