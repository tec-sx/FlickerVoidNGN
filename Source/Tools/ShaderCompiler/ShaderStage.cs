namespace FlickerVoid.Tools.ShaderCompiler;

/// <summary>The pipeline stage a shader entry point runs in.</summary>
public enum ShaderStage : byte
{
    Vertex,
    Pixel,
    Compute,
}
