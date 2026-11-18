using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Reads and writes a <see cref="Vector3"/> as <c>[x, y, z]</c>.</summary>
public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[3];
        JsonFloats.Read(ref reader, values);
        return new Vector3(values);
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        ReadOnlySpan<float> values = [value.X, value.Y, value.Z];
        JsonFloats.Write(writer, values);
    }
}
