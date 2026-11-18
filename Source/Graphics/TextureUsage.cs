namespace FlickerVoid.Engine.Graphics;

/// <summary>What a texture is used for.</summary>
[Flags]
public enum TextureUsage : byte
{
    None = 0,

    /// <summary>Shaders sample it through its bindless index.</summary>
    ShaderRead = 1,
    RenderTarget = 2,
    DepthStencil = 4,
    CopySource = 8,
    CopyDestination = 16,
}
