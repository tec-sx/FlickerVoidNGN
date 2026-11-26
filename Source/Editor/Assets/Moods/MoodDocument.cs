using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Editor.Assets.Moods;

/// <summary>
/// A lighting mood file being edited: the editor draws and edits <see cref="Mood"/> in place, and <see cref="ContentDocument{T}.Hold"/>
/// keeps its values unsaved in the content until they are saved or discarded. It writes only the values the file named and the ones
/// edited since, so what lies under the mood (the environment, the lens) still shows through the rest.
/// </summary>
public sealed class MoodDocument : ContentDocument<LightingMood>
{
    public MoodDocument(UnsavedContent content, string path)
        : this(content, path, new SparseMood())
    {
    }

    private MoodDocument(UnsavedContent content, string path, SparseMood sparse)
        : base(content, path, sparse.Read, sparse.Write)
    {
    }

    public LightingMood Mood => Value;

    /// <summary>Remembers which values a mood file names and what they were, to write back those and the ones changed.</summary>
    private sealed class SparseMood
    {
        private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

        private readonly HashSet<string> _named = [];
        private JsonObject _read = [];

        public LightingMood Read(JsonObject file)
        {
            foreach ((string name, _) in file)
            {
                _named.Add(name);
            }

            LightingMood mood = file.Deserialize(RenderingJsonContext.Default.LightingMood) ?? new LightingMood();
            _read = ToJson(mood);
            return mood;
        }

        public byte[] Write(LightingMood mood, string? schema)
        {
            JsonObject whole = ToJson(mood);
            JsonObject written = [];
            if (schema is not null)
            {
                written[JsonSchemas.SchemaProperty] = schema;
            }

            foreach ((string name, JsonNode? value) in whole)
            {
                if (name == JsonSchemas.SchemaProperty)
                {
                    continue;
                }

                bool changed = !JsonNode.DeepEquals(value, _read[name]);
                if (changed)
                {
                    _named.Add(name);
                }

                if (_named.Contains(name))
                {
                    written[name] = value?.DeepClone();
                }
            }

            _read = whole;
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, WriterOptions))
            {
                written.WriteTo(writer);
            }

            return stream.ToArray();
        }

        private static JsonObject ToJson(LightingMood mood)
        {
            JsonNode? node = JsonSerializer.SerializeToNode(mood, RenderingJsonContext.Default.LightingMood);
            return node?.AsObject() ?? [];
        }
    }
}
