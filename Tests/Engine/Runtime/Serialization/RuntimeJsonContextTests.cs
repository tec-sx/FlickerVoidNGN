using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Serialization;

public class RuntimeJsonContextTests
{
    [Fact]
    public void ReadsVectorsAndQuaternionsAsArrays()
    {
        const string json = """{ "position": [1, 2, 3], "rotation": [0, 0, 0.5, 0.5], "scale": [2, 2, 2] }""";

        Transform transform = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.Transform);

        Vector3 position = new(1, 2, 3);
        Quaternion rotation = new(0, 0, 0.5f, 0.5f);
        Vector3 scale = new(2);
        transform.Position.Should().Be(position);
        transform.Rotation.Should().Be(rotation);
        transform.Scale.Should().Be(scale);
    }

    [Fact]
    public void KeepsDefaultsForMissingProperties()
    {
        Transform transform = JsonSerializer.Deserialize("{}", RuntimeJsonContext.Default.Transform);

        transform.Position.Should().Be(Vector3.Zero);
        transform.Rotation.Should().Be(Quaternion.Identity);
        transform.Scale.Should().Be(Vector3.One);
    }

    [Fact]
    public void WritesVectorsAsArrays()
    {
        Transform transform = new() { Position = new Vector3(1, 2, 3) };

        string json = JsonSerializer.Serialize(transform, RuntimeJsonContext.Default.Transform);

        json.Should().Be("""{"position":[1,2,3],"rotation":[0,0,0,1],"scale":[1,1,1]}""");
    }

    [Fact]
    public void AllowsCommentsAndTrailingCommas()
    {
        const string json = """
            {
              // The room's only entity.
              "entities": [
                { "name": "Crate", },
              ],
            }
            """;

        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);

        scene.Should().NotBeNull();
        scene.Entities.Should().ContainSingle().Which.Name.Should().Be("Crate");
    }

    [Theory]
    [InlineData("""{ "positon": [0, 0, 0] }""")]
    [InlineData("""{ "position": [0, 0] }""")]
    [InlineData("""{ "position": [0, 0, 0, 0] }""")]
    [InlineData("""{ "position": { "x": 0, "y": 0, "z": 0 } }""")]
    public void RejectsMistypedTransforms(string json)
    {
        Action read = () => JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.Transform);

        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void RejectsNullWhereTheTypeIsNotNullable()
    {
        const string json = """{ "entities": null }""";

        Action read = () => JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);

        read.Should().Throw<JsonException>();
    }
}
