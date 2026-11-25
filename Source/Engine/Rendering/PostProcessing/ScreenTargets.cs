
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The textures the scene renderer draws through on the way to its output, sized to it and remade when it changes.</summary>
internal sealed class ScreenTargets : IDisposable
{
    public const PixelFormat HdrFormat = PixelFormat.Rgba16Float;
    public const int BloomLevels = 5;

    private readonly IGraphicsDevice _device;

    public ScreenTargets(IGraphicsDevice device)
    {
        _device = device;
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>The geometry pass draws into it; the screen shadows and lighting passes read it.</summary>
    public ITexture Depth { get; private set; } = null!;

    /// <summary>What the geometry pass leaves at every pixel for lighting.</summary>
    public GBuffer GBuffer { get; private set; } = null!;

    /// <summary>The lit scene before post-processing.</summary>
    public ITexture Hdr { get; private set; } = null!;

    /// <summary>Ambient occlusion in red and contact shadows in green.</summary>
    public ITexture ScreenShadows { get; private set; } = null!;

    /// <summary>Half the size of the HDR target, then halving again; the first ends up holding the whole glow.</summary>
    public ITexture[] Bloom { get; private set; } = [];

    /// <summary>Remakes the textures when the size changes; the device releases old ones once the GPU is done with them.</summary>
    public void Ensure(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return;
        }

        Dispose();
        Width = width;
        Height = height;
        TextureDescription hdr = new(width, height, HdrFormat, TextureUsage.ShaderRead | TextureUsage.ShaderWrite) { Name = "Scene HDR" };
        Hdr = _device.CreateTexture(hdr);
        TextureDescription depth = new(width, height, SceneRenderer.DepthFormat, TextureUsage.DepthStencil | TextureUsage.ShaderRead) { Name = "Scene depth" };
        Depth = _device.CreateTexture(depth);
        GBuffer = new GBuffer(_device, width, height);
        TextureDescription screenShadows = new(width, height, PixelFormat.Rgba8UNorm, TextureUsage.ShaderWrite | TextureUsage.ShaderRead) { Name = "Screen shadows" };
        ScreenShadows = _device.CreateTexture(screenShadows);
        Bloom = new ITexture[BloomLevels];
        for (int level = 0; level < BloomLevels; level++)
        {
            int levelWidth = Math.Max(1, width >> (level + 1));
            int levelHeight = Math.Max(1, height >> (level + 1));
            TextureDescription bloom = new(levelWidth, levelHeight, HdrFormat, TextureUsage.ShaderWrite | TextureUsage.ShaderRead) { Name = $"Bloom {level}" };
            Bloom[level] = _device.CreateTexture(bloom);
        }
    }

    public void Dispose()
    {
        foreach (ITexture level in Bloom)
        {
            level.Dispose();
        }

        Bloom = [];
        ScreenShadows?.Dispose();
        GBuffer?.Dispose();
        Depth?.Dispose();
        Hdr?.Dispose();
    }
}

