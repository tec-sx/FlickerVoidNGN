namespace FlickerVoid.Engine.Graphics.Textures;

/// <summary>Texture and render target formats; the values are DXGI's.</summary>
public enum PixelFormat
{
    None = 0,
    Rgba32Float = 2,
    Rgba16Float = 10,
    Rg11B10Float = 26,
    Rgba8UNorm = 28,
    Rgba8UNormSrgb = 29,
    Rg16Float = 34,
    Depth32Float = 40,
    R32Float = 41,
    Depth24UNormStencil8 = 45,
    R16Float = 54,
    Depth16UNorm = 55,
    R8UNorm = 61,
    Bgra8UNorm = 87,
    Bgra8UNormSrgb = 91,
}

