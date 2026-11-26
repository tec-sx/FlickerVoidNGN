using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// The textures the scene renderer draws through on the way to its output: the scene's at the render size, and the upscaled
/// scene and bloom at the output's size; remade when either size changes or upscaling starts or stops.
/// </summary>
internal sealed class ScreenTargets : IDisposable
{
    public const PixelFormat HdrFormat = PixelFormat.Rgba16Float;
    private const int MaxBloomLevels = 8;

    /// <summary>How many levels the bloom has for an output this high: the smallest stays a few pixels, so the glow keeps its size on screen at any resolution (8 at 1080p).</summary>
    public static int BloomLevelsFor(int outputHeight)
    {
        int levels = (int)Math.Floor(Math.Log2(Math.Max(outputHeight, 1))) - 2;
        return Math.Clamp(levels, 1, MaxBloomLevels);
    }

    private readonly IGraphicsDevice _device;

    public ScreenTargets(IGraphicsDevice device)
    {
        _device = device;
    }

    /// <summary>The render width: the size of the scene's textures.</summary>
    public int Width { get; private set; }

    /// <summary>The render height: the size of the scene's textures.</summary>
    public int Height { get; private set; }

    public int OutputWidth { get; private set; }

    public int OutputHeight { get; private set; }

    /// <summary>The geometry pass draws into it; the screen shadows and lighting passes read it.</summary>
    public ITexture Depth { get; private set; } = null!;

    /// <summary>What the geometry pass leaves at every pixel for lighting.</summary>
    public GBuffer GBuffer { get; private set; } = null!;

    /// <summary>The lit scene before post-processing.</summary>
    public ITexture Hdr { get; private set; } = null!;

    /// <summary>The lit scene an upscaler filled in at the output's size, or null while the scene is drawn at it.</summary>
    public ITexture? Upscaled { get; private set; }

    /// <summary>The lit scene at the output's size, which post-processing reads: the upscaled one, else the scene's own.</summary>
    public ITexture Lit => Upscaled ?? Hdr;

    /// <summary>Ambient occlusion in red and contact shadows in green.</summary>
    public ITexture ScreenShadows { get; private set; } = null!;

    /// <summary>Half the output's size, then halving again; the first ends up holding the whole glow.</summary>
    public ITexture[] Bloom { get; private set; } = [];

    /// <summary>
    /// Remakes the textures when a size changes or upscaling starts or stops, and says whether it did; the device releases old
    /// ones once the GPU is done with them.
    /// </summary>
    public bool Ensure(int width, int height, int outputWidth, int outputHeight, bool upscaled)
    {
        bool wasUpscaled = Upscaled is not null;
        if (width == Width && height == Height && outputWidth == OutputWidth && outputHeight == OutputHeight && upscaled == wasUpscaled)
        {
            return false;
        }

        Dispose();
        Width = width;
        Height = height;
        OutputWidth = outputWidth;
        OutputHeight = outputHeight;
        TextureUsage readWrite = TextureUsage.ShaderRead | TextureUsage.ShaderWrite;
        TextureDescription hdr = new(width, height, HdrFormat, readWrite) { Name = "Scene HDR" };
        Hdr = _device.CreateTexture(hdr);
        TextureDescription depth = new(width, height, SceneRenderer.DepthFormat, TextureUsage.DepthStencil | TextureUsage.ShaderRead) { Name = "Scene depth" };
        Depth = _device.CreateTexture(depth);
        GBuffer = new GBuffer(_device, width, height);
        TextureDescription screenShadows = new(width, height, PixelFormat.Rgba8UNorm, readWrite) { Name = "Screen shadows" };
        ScreenShadows = _device.CreateTexture(screenShadows);
        if (upscaled)
        {
            TextureDescription upscaledDescription = new(outputWidth, outputHeight, HdrFormat, readWrite | TextureUsage.CopyDestination) { Name = "Upscaled HDR" };
            Upscaled = _device.CreateTexture(upscaledDescription);
        }

        int bloomLevels = BloomLevelsFor(outputHeight);
        Bloom = new ITexture[bloomLevels];
        for (int level = 0; level < bloomLevels; level++)
        {
            int levelWidth = Math.Max(1, outputWidth >> (level + 1));
            int levelHeight = Math.Max(1, outputHeight >> (level + 1));
            TextureDescription bloom = new(levelWidth, levelHeight, HdrFormat, readWrite) { Name = $"Bloom {level}" };
            Bloom[level] = _device.CreateTexture(bloom);
        }

        return true;
    }

    public void Dispose()
    {
        foreach (ITexture level in Bloom)
        {
            level.Dispose();
        }

        Bloom = [];
        Upscaled?.Dispose();
        Upscaled = null;
        ScreenShadows?.Dispose();
        GBuffer?.Dispose();
        Depth?.Dispose();
        Hdr?.Dispose();
    }
}
