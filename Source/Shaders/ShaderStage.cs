namespace FlickerVoid.Engine.Shaders;

/// <summary>The pipeline stage a shader entry point runs in.</summary>
public enum ShaderStage : byte
{
    Vertex,
    Pixel,
    Compute,
}
