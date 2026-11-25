using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Tests.Solids;

public sealed class SolidMeshJsonConverterTests
{
    /// <summary>A box with its top face left out, so the edges round the top meet one face each.</summary>
    private const string OpenBox = """
        {
          "points": [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]],
          "faces": [[0, 3, 2, 1], [4, 5, 6, 7], [0, 4, 7, 3], [1, 2, 6, 5], [0, 1, 5, 4]]
        }
        """;

    [Fact]
    public void WritesPointsAndFacesAndReadsThemBack()
    {
        Vector3 size = new(2, 1, 3);
        SolidMesh box = SolidMesh.Box(size);

        string json = JsonSerializer.Serialize(box, RuntimeJsonContext.Default.SolidMesh);
        SolidMesh? read = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SolidMesh);

        json.Should().StartWith("""{"points":[[-1,0,-1.5],""").And.Contain("""],"faces":[[5,1,3,7],""");
        read.Should().NotBeNull();
        read!.Points.Should().Equal(box.Points);
        read.Faces.Should().HaveCount(6);
        read.Faces[0].Should().Equal(box.Faces[0]);
    }

    [Fact]
    public void RefusesAnOpenSolidAsUnreadable()
    {
        Action read = () => JsonSerializer.Deserialize(OpenBox, RuntimeJsonContext.Default.SolidMesh);

        read.Should().Throw<JsonException>().WithMessage("The solid is open*");
    }

    [Theory]
    [InlineData("""{ "points": [[0, 0, 0]] }""", "A solid needs its points and faces*")]
    [InlineData("""{ "points": [], "faces": [], "edges": [] }""", "A solid has no 'edges'*")]
    [InlineData("""{ "points": [[0, 0]], "faces": [] }""", "*")]
    public void RefusesWhatIsNotASolid(string json, string message)
    {
        Action read = () => JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SolidMesh);

        read.Should().Throw<JsonException>().WithMessage(message);
    }

    [Fact]
    public void DescribesASolidInItsSchema()
    {
        JsonObject schema = JsonSchemas.Create(RuntimeJsonContext.Default.SolidMesh);

        schema["type"]!.GetValue<string>().Should().Be("object");
        schema["required"]!.AsArray().Select(name => name!.GetValue<string>()).Should().Equal("points", "faces");
        schema["properties"]!["points"]!["items"]!["maxItems"]!.GetValue<int>().Should().Be(3);
        schema["properties"]!["faces"]!["items"]!["minItems"]!.GetValue<int>().Should().Be(3);
        schema["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
    }
}
