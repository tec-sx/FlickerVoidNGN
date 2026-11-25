using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>
/// Reads and writes a <see cref="SolidMesh"/> as <c>{ "points": [[x, y, z], ...], "faces": [[0, 1, 2], ...] }</c>; a solid that
/// isn't closed can't be read, and the reason is the error's message.
/// </summary>
public sealed class SolidMeshJsonConverter : JsonConverter<SolidMesh>
{
    public override SolidMesh Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Expect(reader, JsonTokenType.StartObject);
        List<Vector3>? points = null;
        List<int[]>? faces = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string? name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "points":
                    points = ReadPoints(ref reader);
                    break;
                case "faces":
                    faces = ReadFaces(ref reader);
                    break;
                default:
                    throw new JsonException($"A solid has no '{name}', only points and faces.");
            }
        }

        if (points is null || faces is null)
        {
            throw new JsonException("A solid needs its points and faces.");
        }

        try
        {
            return new SolidMesh(points, faces);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, SolidMesh value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("points");
        foreach (Vector3 point in value.Points)
        {
            ReadOnlySpan<float> values = [point.X, point.Y, point.Z];
            JsonFloats.Write(writer, values);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("faces");
        foreach (IReadOnlyList<int> face in value.Faces)
        {
            writer.WriteStartArray();
            foreach (int point in face)
            {
                writer.WriteNumberValue(point);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static List<Vector3> ReadPoints(ref Utf8JsonReader reader)
    {
        Expect(reader, JsonTokenType.StartArray);
        List<Vector3> points = [];
        Span<float> values = stackalloc float[3];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            JsonFloats.Read(ref reader, values);
            Vector3 point = new(values);
            points.Add(point);
        }

        return points;
    }

    private static List<int[]> ReadFaces(ref Utf8JsonReader reader)
    {
        Expect(reader, JsonTokenType.StartArray);
        List<int[]> faces = [];
        List<int> face = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            Expect(reader, JsonTokenType.StartArray);
            face.Clear();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                Expect(reader, JsonTokenType.Number);
                face.Add(reader.GetInt32());
            }

            faces.Add([.. face]);
        }

        return faces;
    }

    private static void Expect(in Utf8JsonReader reader, JsonTokenType token)
    {
        if (reader.TokenType != token)
        {
            throw new JsonException($"A solid's JSON has {reader.TokenType} where {token} belongs.");
        }
    }
}
