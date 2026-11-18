using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Reads and writes a <see cref="Quaternion"/> as <c>[x, y, z, w]</c>.</summary>
public sealed class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[4];
        JsonFloats.Read(ref reader, values);
        return new Quaternion(values[0], values[1], values[2], values[3]);
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        ReadOnlySpan<float> values = [value.X, value.Y, value.Z, value.W];
        JsonFloats.Write(writer, values);
    }
}
