using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>Reads a mood file as the values it names, by the mood's own rules, and writes only those back.</summary>
public sealed class MoodOverrideJsonConverter : JsonConverter<MoodOverride>
{
    public override MoodOverride Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        JsonNode? node = JsonNode.Parse(ref reader);
        if (node is not JsonObject file)
        {
            throw new JsonException("A mood is an object of the values it changes.");
        }

        LightingMood? values = file.Deserialize(RenderingJsonContext.Default.LightingMood);
        if (values is null)
        {
            throw new JsonException("A mood is an object of the values it changes.");
        }

        List<MoodValue> named = [];
        foreach (KeyValuePair<string, JsonNode?> property in file)
        {
            MoodValue? value = MoodValues.Find(property.Key);
            if (value is not null)
            {
                named.Add(value);
            }
        }

        return new MoodOverride(values, named);
    }

    public override void Write(Utf8JsonWriter writer, MoodOverride value, JsonSerializerOptions options)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(value.Values, RenderingJsonContext.Default.LightingMood);
        JsonObject all = node!.AsObject();
        writer.WriteStartObject();
        foreach (MoodValue named in value.Named)
        {
            writer.WritePropertyName(named.Name);
            JsonNode? property = all[named.Name];
            if (property is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }
}
