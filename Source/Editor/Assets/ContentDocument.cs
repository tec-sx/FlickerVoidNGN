using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// A content file being edited as an object: read from the file's current contents, unsaved edits included, into an object nothing
/// else holds and edited in place, then kept unsaved by <see cref="Hold"/> until it is saved or discarded. Each hold is one undo step
/// in the file's history and a change every load of the file sees, as after a change on disk.
/// </summary>
public abstract class ContentDocument<T>
    where T : class
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly UnsavedContent _content;
    private readonly Func<T, string?, byte[]> _write;
    private readonly string? _schema;

    /// <summary>Reads a JSON file of the type <paramref name="typeInfo"/> describes, and writes it back the same way.</summary>
    protected ContentDocument(UnsavedContent content, string path, JsonTypeInfo<T> typeInfo)
    {
        _content = content;
        _write = (value, _) => Serialize(value, typeInfo);
        Path = path;
        Value = LoadFresh(content, path, typeInfo);
    }

    /// <summary>
    /// Reads the file's JSON into an object with <paramref name="read"/>, and writes the object back with <paramref name="write"/>,
    /// given the schema the file names; throws <see cref="InvalidDataException"/> when the file can't be read.
    /// </summary>
    protected ContentDocument(UnsavedContent content, string path, Func<JsonObject, T> read, Func<T, string?, byte[]> write)
    {
        _content = content;
        _write = write;
        Path = path;
        JsonObject file = LoadFresh(content, path, RuntimeJsonContext.Default.JsonObject);
        _schema = file[JsonSchemas.SchemaProperty]?.GetValue<string>();
        Value = read(file);
    }

    public string Path { get; }

    /// <summary>The file's contents as edited.</summary>
    protected T Value { get; }

    /// <summary>Keeps the object as the file's unsaved contents.</summary>
    public void Hold()
    {
        byte[] contents = _write(Value, _schema);
        _content.Edit(Path, contents);
    }

    // Unloading around the load reads the file's current contents, unsaved edits included, into an object nothing else holds.
    private static TLoaded LoadFresh<TLoaded>(UnsavedContent content, string path, JsonTypeInfo<TLoaded> typeInfo)
        where TLoaded : class
    {
        content.Unload(path);
        TLoaded loaded = content.Load(path, typeInfo);
        content.Unload(path);
        return loaded;
    }

    private static byte[] Serialize(T value, JsonTypeInfo<T> typeInfo)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, value, typeInfo);
        }

        return stream.ToArray();
    }
}
