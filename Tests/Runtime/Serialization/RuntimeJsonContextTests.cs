using System.Numerics;
using System.Text.Json;

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
        Assert.Equal(position, transform.Position);
        Assert.Equal(rotation, transform.Rotation);
        Assert.Equal(scale, transform.Scale);
    }

    [Fact]
    public void KeepsDefaultsForMissingProperties()
    {
        Transform transform = JsonSerializer.Deserialize("{}", RuntimeJsonContext.Default.Transform);

        Assert.Equal(Vector3.Zero, transform.Position);
        Assert.Equal(Quaternion.Identity, transform.Rotation);
        Assert.Equal(Vector3.One, transform.Scale);
    }

    [Fact]
    public void WritesVectorsAsArrays()
    {
        Transform transform = new() { Position = new Vector3(1, 2, 3) };

        string json = JsonSerializer.Serialize(transform, RuntimeJsonContext.Default.Transform);

        Assert.Equal("""{"position":[1,2,3],"rotation":[0,0,0,1],"scale":[1,1,1]}""", json);
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

        Assert.NotNull(scene);
        Assert.Equal("Crate", scene.Entities[0].Name);
    }

    [Theory]
    [InlineData("""{ "positon": [0, 0, 0] }""")]
    [InlineData("""{ "position": [0, 0] }""")]
    [InlineData("""{ "position": [0, 0, 0, 0] }""")]
    [InlineData("""{ "position": { "x": 0, "y": 0, "z": 0 } }""")]
    public void RejectsMistypedTransforms(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.Transform));
    }

    [Fact]
    public void RejectsNullWhereTheTypeIsNotNullable()
    {
        const string json = """{ "entities": null }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData));
    }
}
