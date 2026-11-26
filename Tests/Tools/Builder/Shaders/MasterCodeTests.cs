using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Tools.Builder.Shaders;

namespace FlickerVoid.Tools.Builder.Tests.Shaders;

public sealed class MasterCodeTests
{
    private const string Declared = """
        {
          "parameters": [
            { "name": "mortar", "type": "float", "default": 0 },
            { "name": "grime", "type": "color", "default": [0, 0, 0] },
            { "name": "dirt", "type": "texture" },
            { "name": "wear", "type": "float", "default": 0 }
          ]
        }
        """;

    private readonly IReadOnlyList<ShaderParameter> _parameters = JsonSerializer.Deserialize(Declared, RenderingJsonContext.Default.ShaderParameters)!.Parameters;

    [Fact]
    public void LoadsEachParameterAfterTheSceneShadersInTheOrderTheyPack()
    {
        string code = MasterCode.Generate("Brick.hlsl", _parameters, 96);

        code.Should().Contain("parameters.Mortar = buffer.Load<float>(96);")
            .And.Contain("parameters.Grime = buffer.Load<float3>(100);")
            .And.Contain("parameters.Dirt = buffer.Load<uint>(112);")
            .And.Contain("parameters.DirtRelief = buffer.Load<uint>(116);")
            .And.Contain("parameters.Wear = buffer.Load<float>(120);");
    }

    [Fact]
    public void IncludesTheMastersSurfaceBetweenTheSceneShadersCodeAndTheGeometryPass()
    {
        string code = MasterCode.Generate("Brick.hlsl", _parameters, 96);

        int scene = code.IndexOf("#include \"SceneSurface.hlsl\"", StringComparison.Ordinal);
        int master = code.IndexOf("#include \"Brick.hlsl\"", StringComparison.Ordinal);
        int geometry = code.IndexOf("#include \"Geometry.hlsl\"", StringComparison.Ordinal);
        scene.Should().BeGreaterThanOrEqualTo(0);
        master.Should().BeGreaterThan(scene);
        geometry.Should().BeGreaterThan(master);
    }
}
