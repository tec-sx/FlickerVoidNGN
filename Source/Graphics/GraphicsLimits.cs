namespace FlickerVoid.Engine.Graphics;

/// <summary>Limits every backend meets, so code that stays within them runs on all of them.</summary>
public static class GraphicsLimits
{
    /// <summary>Bytes of push constants a draw gets; the minimum Vulkan guarantees.</summary>
    public const int PushConstantSize = 128;

    /// <summary>Row pitch alignment of buffer-texture copies; Vulkan doesn't need it, but it keeps copies on the alignment drivers prefer.</summary>
    public const int TextureRowPitchAlignment = 256;

    /// <summary>Bindless slots for textures and buffers, which share one index range.</summary>
    public const int BindlessResourceCapacity = 65536;

    /// <summary>Bindless slots for samplers.</summary>
    public const int BindlessSamplerCapacity = 256;
}
