using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// The parameters a shader declares, which the content build writes to <c>Name.fvparams</c> beside its compiled
/// stages; materials pack their values in this order (see <see cref="MaterialLayout"/>).
/// </summary>
public sealed record ShaderParameters
{
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public List<ShaderParameter> Parameters { get; init; } = [];

    /// <summary>The parameters the content build wrote for the shader, or none when it declares none.</summary>
    public static ShaderParameters Load(ShaderLibrary shaders, string shader)
    {
        string path = Path.Combine(shaders.Directory, $"{shader}.fvparams");
        if (!File.Exists(path))
        {
            return new ShaderParameters();
        }

        byte[] json = File.ReadAllBytes(path);
        ShaderParameters? parameters = JsonSerializer.Deserialize(json, RenderingJsonContext.Default.ShaderParameters);
        return parameters ?? new ShaderParameters();
    }
}
