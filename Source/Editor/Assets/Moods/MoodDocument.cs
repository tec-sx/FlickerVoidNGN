using System.Text.Json;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Assets.Moods;

/// <summary>
/// A lighting mood file being edited: the editor draws and edits <see cref="Mood"/> in place, and <see cref="Hold"/> keeps its
/// values unsaved in the content until they are saved or discarded.
/// </summary>
public sealed class MoodDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly UnsavedContent _content;

    public MoodDocument(UnsavedContent content, string path)
    {
        _content = content;
        Path = path;

        // Unloading before and after loading gives the document a mood nothing else holds, so editing it in place changes no
        // mood loaded elsewhere; other loaders see the edits once they are held.
        _content.Unload(path);
        Mood = _content.Load(path, RenderingJsonContext.Default.LightingMood);
        _content.Unload(path);
    }

    public string Path { get; }

    public LightingMood Mood { get; }

    /// <summary>Keeps the mood's values as the file's unsaved contents.</summary>
    public void Hold()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            JsonSerializer.Serialize(writer, Mood, RenderingJsonContext.Default.LightingMood);
        }

        byte[] contents = stream.ToArray();
        _content.Edit(Path, contents);
    }
}
