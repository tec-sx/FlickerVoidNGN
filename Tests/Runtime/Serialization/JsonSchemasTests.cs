using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Serialization;

public class JsonSchemasTests
{
    [Fact]
    public void DescribesVectorsAndQuaternionsAsArraysOfNumbers()
    {
        JsonObject schema = JsonSchemas.Create(RuntimeJsonContext.Default.Transform);

        JsonNode position = schema["properties"]!["position"]!;
        JsonNode rotation = schema["properties"]!["rotation"]!;
        position["items"]!["type"]!.GetValue<string>().Should().Be("number");
        position["maxItems"]!.GetValue<int>().Should().Be(3);
        rotation["minItems"]!.GetValue<int>().Should().Be(4);
    }

    [Fact]
    public void LetsAFileNameItsSchema()
    {
        JsonObject schema = JsonSchemas.CreateFile(RuntimeJsonContext.Default.Transform);

        schema["properties"]!["$schema"]!["type"]!.GetValue<string>().Should().Be("string");
        schema["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
    }
}
