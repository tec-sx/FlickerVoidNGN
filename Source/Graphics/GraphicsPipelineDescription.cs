namespace FlickerVoid.Engine.Graphics;

/// <summary>
/// Shaders and fixed-function state of a draw. There is no vertex input: vertex shaders read their vertices from
/// bindless buffers with SV_VertexID, and resources come through push constants holding bindless indices.
/// </summary>
public sealed record GraphicsPipelineDescription
{
    public required ShaderBytecode VertexShader { get; init; }

    public required ShaderBytecode PixelShader { get; init; }

    public required IReadOnlyList<PixelFormat> ColorFormats { get; init; }

    public PixelFormat DepthFormat { get; init; }

    public PrimitiveTopology Topology { get; init; }

    public CullMode CullMode { get; init; } = CullMode.Back;

    public DepthState Depth { get; init; } = DepthState.Disabled;

    /// <summary>Applied to every color target.</summary>
    public BlendMode Blend { get; init; }

    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
