using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Engine.Rendering.Tests.Materials;

/// <summary>Builds the parameters a shader declares and the material values to pack with them.</summary>
internal sealed class MaterialLayoutTestsBuilder
{
    private const string Declared = """
        {
          "parameters": [
            { "name": "wrap", "type": "float", "default": 0.25 },
            { "name": "emissive", "type": "color", "default": [0, 0, 0] }
          ]
        }
        """;

    public IReadOnlyList<ShaderParameter> Parameters { get; } = JsonSerializer.Deserialize(Declared, RenderingJsonContext.Default.ShaderParameters)!.Parameters;

    public static MaterialFile Material(string json)
    {
        return JsonSerializer.Deserialize(json, RenderingJsonContext.Default.MaterialFile)!;
    }
}
