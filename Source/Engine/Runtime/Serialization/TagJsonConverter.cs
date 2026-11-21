using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Reads and writes a <see cref="Tag"/> as its name.</summary>
public sealed class TagJsonConverter : JsonConverter<Tag>
{
    public override Tag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? name = reader.GetString();
        if (string.IsNullOrEmpty(name))
        {
            return Tag.None;
        }

        return new Tag(name);
    }

    public override void Write(Utf8JsonWriter writer, Tag value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Name);
    }
}
