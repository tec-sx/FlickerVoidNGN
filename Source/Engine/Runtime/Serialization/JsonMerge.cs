using System.Buffers;
using System.Text.Json;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Lays JSON values over each other, object property by property, and finds what one value changes in another.</summary>
public static class JsonMerge
{
    /// <summary><paramref name="over"/> laid on <paramref name="under"/>: objects merge property by property, anything else is replaced.</summary>
    public static JsonElement Merge(JsonElement under, JsonElement over)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            WriteMerged(writer, under, over);
        }

        return Parse(buffer);
    }

    /// <summary>The part of <paramref name="value"/> that differs from <paramref name="under"/>, or null when nothing does.</summary>
    public static JsonElement? Diff(JsonElement under, JsonElement value)
    {
        if (under.ValueKind != JsonValueKind.Object || value.ValueKind != JsonValueKind.Object)
        {
            if (JsonElement.DeepEquals(under, value))
            {
                return null;
            }

            return value;
        }

        ArrayBufferWriter<byte> buffer = new();
        bool differs = false;
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject())
            {
                JsonElement? changed = property.Value;
                if (under.TryGetProperty(property.Name, out JsonElement old))
                {
                    changed = Diff(old, property.Value);
                }

                if (changed is { } difference)
                {
                    writer.WritePropertyName(property.Name);
                    difference.WriteTo(writer);
                    differs = true;
                }
            }

            writer.WriteEndObject();
        }

        if (!differs)
        {
            return null;
        }

        return Parse(buffer);
    }

    private static void WriteMerged(Utf8JsonWriter writer, JsonElement under, JsonElement over)
    {
        if (under.ValueKind != JsonValueKind.Object || over.ValueKind != JsonValueKind.Object)
        {
            over.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (JsonProperty property in under.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            if (over.TryGetProperty(property.Name, out JsonElement replacement))
            {
                WriteMerged(writer, property.Value, replacement);
            }
            else
            {
                property.Value.WriteTo(writer);
            }
        }

        foreach (JsonProperty property in over.EnumerateObject())
        {
            if (!under.TryGetProperty(property.Name, out _))
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static JsonElement Parse(ArrayBufferWriter<byte> buffer)
    {
        Utf8JsonReader reader = new(buffer.WrittenSpan);
        return JsonElement.ParseValue(ref reader);
    }
}
