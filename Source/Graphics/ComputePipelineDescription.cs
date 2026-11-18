namespace FlickerVoid.Engine.Graphics;

/// <summary>A compute shader; like draws, it reads and writes resources through push constants holding bindless indices.</summary>
public sealed record ComputePipelineDescription
{
    public required ShaderBytecode ComputeShader { get; init; }

    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
