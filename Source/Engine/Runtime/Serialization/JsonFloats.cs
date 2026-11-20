using System.Text.Json;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Reads and writes a fixed number of floats as a JSON array, for vector-like converters.</summary>
internal static class JsonFloats
{
    public static void Read(ref Utf8JsonReader reader, scoped Span<float> values)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException();
        }

        for (int i = 0; i < values.Length; i++)
        {
            reader.Read();
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException();
            }

            values[i] = reader.GetSingle();
        }

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException();
        }
    }

    public static void Write(Utf8JsonWriter writer, ReadOnlySpan<float> values)
    {
        writer.WriteStartArray();
        foreach (float value in values)
        {
            writer.WriteNumberValue(value);
        }

        writer.WriteEndArray();
    }
}
