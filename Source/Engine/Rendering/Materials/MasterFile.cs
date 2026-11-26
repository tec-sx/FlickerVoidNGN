using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A <c>Name.fvmaster</c> file, a master material: how light treats its surfaces and the parameters of its own that its
/// instances set, after the scene shader's, read by the <c>SurfaceOf</c> function of the <c>Name.hlsl</c> beside it, which the
/// content build compiles into the master's geometry pass (<c>Name.GeometryMain.spv</c>).
/// </summary>
public sealed record MasterFile
{
    public const string Extension = ".fvmaster";

    /// <summary>The geometry pass's pixel shader the content build compiles for a master, beside its file.</summary>
    public const string GeometryEntryPoint = "GeometryMain";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public ShadingModel ShadingModel { get; init; }

    public List<ShaderParameter> Parameters { get; set; } = [];

    /// <summary>The content path of a master's compiled geometry pass.</summary>
    public static string GetGeometryPath(string masterPath)
    {
        return $"{masterPath[..^Extension.Length]}.{GeometryEntryPoint}.spv";
    }
}
