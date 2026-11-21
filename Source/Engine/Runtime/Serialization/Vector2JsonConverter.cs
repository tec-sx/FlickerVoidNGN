using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Reads and writes a <see cref="Vector2"/> as <c>[x, y]</c>.</summary>
public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[2];
        JsonFloats.Read(ref reader, values);
        return new Vector2(values);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        ReadOnlySpan<float> values = [value.X, value.Y];
        JsonFloats.Write(writer, values);
    }
}
