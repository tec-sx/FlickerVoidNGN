using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A <c>Name.fvmaster</c> file, a master material: how light treats its surfaces and the parameters of its own that its
/// instances set, after the scene shader's, read by the <c>SurfaceOf</c> function of the <c>Name.hlsl</c> beside it, which the
/// content build compiles into the master's geometry pass (<c>Name.GeometryMain.spv</c>), or for glass its transparent pass
/// (<c>Name.TransparentMain.spv</c>).
/// </summary>
public sealed record MasterFile
{
    public const string Extension = ".fvmaster";

    /// <summary>The geometry pass's pixel shader the content build compiles for a master, beside its file.</summary>
    public const string GeometryEntryPoint = "GeometryMain";

    /// <summary>The transparent pass's pixel shader the content build compiles in its place for a master of glass.</summary>
    public const string TransparentEntryPoint = "TransparentMain";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public ShadingModel ShadingModel { get; init; }

    /// <summary>Whether both sides of its surfaces are drawn, the back's normal turned toward the camera, as cards of leaves need.</summary>
    public bool TwoSided { get; init; }

    public List<ShaderParameter> Parameters { get; set; } = [];

    /// <summary>The pixel shader the content build compiles for the master: the transparent pass's for glass, else the geometry pass's.</summary>
    [JsonIgnore]
    public string EntryPoint
    {
        get
        {
            if (ShadingModels.IsTransparent(ShadingModel))
            {
                return TransparentEntryPoint;
            }

            return GeometryEntryPoint;
        }
    }

    /// <summary>The content path of a master's compiled pixel shader for the entry point.</summary>
    public static string GetShaderPath(string masterPath, string entryPoint)
    {
        return $"{masterPath[..^Extension.Length]}.{entryPoint}.spv";
    }

    /// <summary>The content path of a master's compiled geometry pass.</summary>
    public static string GetGeometryPath(string masterPath)
    {
        return GetShaderPath(masterPath, GeometryEntryPoint);
    }
}
