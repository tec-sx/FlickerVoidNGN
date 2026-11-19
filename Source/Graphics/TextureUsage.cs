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

    /// <summary>Shaders write it as a RWTexture2D through its bindless index, the same index sampling uses.</summary>
    ShaderWrite = 32,
}
