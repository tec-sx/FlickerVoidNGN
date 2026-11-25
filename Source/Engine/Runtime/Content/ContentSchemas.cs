using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>
/// The JSON Schemas of a game's text content, by name (<c>scene</c>, <c>mood</c>), which modules add as they register what files
/// may hold. A development build writes them into each source content folder's <see cref="FolderName"/>, where content files
/// name them with <c>"$schema": ".schemas/scene.schema.json"</c> (relative to the file) and text editors check and complete them.
/// Kinds of files that hold one value, such as moods, are added with their extension, so a <see cref="ContentCheck"/> reads them too.
/// </summary>
public sealed class ContentSchemas
{
    public const string FolderName = ".schemas";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly Dictionary<string, Func<JsonObject>> _schemas = [];
    private readonly Dictionary<string, Func<JsonTypeInfo>> _files = [];

    /// <summary>The type info reading each kind of file added with <see cref="AddFile"/>, by extension.</summary>
    public IReadOnlyDictionary<string, Func<JsonTypeInfo>> Files => _files;

    public void Add(string name, Func<JsonObject> create)
    {
        _schemas.Add(name, create);
    }

    /// <summary>Adds the schema of the files with the extension, each holding one value the type info reads.</summary>
    public void AddFile(string name, string extension, Func<JsonTypeInfo> typeInfo)
    {
        AddFile(name, extension, typeInfo, () => JsonSchemas.CreateFile(typeInfo()));
    }

    /// <summary>Adds the files with the extension, read by the type info, with a schema of their own, for files a converter reads.</summary>
    public void AddFile(string name, string extension, Func<JsonTypeInfo> typeInfo, Func<JsonObject> schema)
    {
        _files.Add(extension, typeInfo);
        _schemas.Add(name, schema);
    }

    /// <summary>Writes every schema into the source folder's schema folder as <c>name.schema.json</c>, leaving unchanged files alone.</summary>
    public void Write(string sourceFolder)
    {
        string folder = Path.Combine(sourceFolder, FolderName);
        Directory.CreateDirectory(folder);
        foreach ((string name, Func<JsonObject> create) in _schemas)
        {
            JsonObject schema = create();
            string json = schema.ToJsonString(WriteOptions);
            string path = Path.Combine(folder, $"{name}.schema.json");
            if (File.Exists(path) && File.ReadAllText(path) == json)
            {
                continue;
            }

            File.WriteAllText(path, json);
        }
    }
}
