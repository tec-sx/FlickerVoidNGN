using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>Reads a lens file as the values it names, refusing what is not the lens's, so a misspelt or misplaced value is not silently ignored.</summary>
public sealed class CameraLensJsonConverter : JsonConverter<CameraLens>
{
    public override CameraLens Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonNode? node = JsonNode.Parse(ref reader);
        if (node is not JsonObject file)
        {
            throw new JsonException("A lens is an object of the values it sets.");
        }

        foreach (KeyValuePair<string, JsonNode?> property in file)
        {
            Require(property.Key);
        }

        MoodOverride values = file.Deserialize(RenderingJsonContext.Default.MoodOverride)!;
        return new CameraLens(values);
    }

    public override void Write(Utf8JsonWriter writer, CameraLens value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Values, RenderingJsonContext.Default.MoodOverride);
    }

    private static void Require(string name)
    {
        if (name == JsonSchemas.SchemaProperty)
        {
            return;
        }

        MoodValue? value = MoodValues.Find(name);
        if (value is null)
        {
            throw new JsonException($"No mood value is named '{name}'.");
        }

        if (value.Owner != MoodOwner.Lens)
        {
            throw new JsonException($"'{name}' belongs to the environment, so a lens cannot hold it.");
        }
    }
}
