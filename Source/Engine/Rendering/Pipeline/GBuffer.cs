using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// What the geometry pass leaves at every pixel for the passes after it to light and tone, beside the scene's depth: the surface's
/// unlit color and coverage, its normal, its material, paint thickness and flags, its object's depth and how far it moved since
/// the frame before. Sized to the output.
/// </summary>
public sealed class GBuffer : IDisposable
{
    internal GBuffer(IGraphicsDevice device, int width, int height)
    {
        Albedo = Create(device, width, height, Formats[0], "G-buffer albedo");
        Normal = Create(device, width, height, Formats[1], "G-buffer normal");
        Material = Create(device, width, height, Formats[2], "G-buffer material");
        ObjectDepth = Create(device, width, height, Formats[3], "G-buffer object depth");
        Motion = Create(device, width, height, Formats[4], "G-buffer motion");
        Targets = [Albedo, Normal, Material, ObjectDepth, Motion];
    }

    /// <summary>The targets' formats, in the order the geometry pass writes them: albedo, normal, material, object depth and motion.</summary>
    public static IReadOnlyList<PixelFormat> Formats { get; } =
    [
        PixelFormat.Rgba8UNormSrgb,
        PixelFormat.Rg16Float,
        PixelFormat.Rgba8UNorm,
        PixelFormat.R16Float,
        PixelFormat.Rg16Float,
    ];

    /// <summary>The surface's color before any light, and in alpha how much of the pixel it covers.</summary>
    public ITexture Albedo { get; }

    /// <summary>The surface's world normal, folded onto an octahedron so two channels hold it.</summary>
    public ITexture Normal { get; }

    /// <summary>
    /// Four bytes: the low and high byte of the surface's material (its buffer's bindless index), how thick its paint stands
    /// (a painting's impasto, 1 where it hides the canvas) and its mesh's <see cref="Extraction.MeshFlags"/>.
    /// </summary>
    public ITexture Material { get; }

    /// <summary>
    /// The view depth the pixel's object tones at: a small object's box centre, so it tones whole, the pixel's own on a big one such
    /// as the ground, and between the two for sizes between (<see cref="Meshes.ObjectDepth"/>).
    /// </summary>
    public ITexture ObjectDepth { get; }

    /// <summary>Where the pixel's surface stood in the frame before less where it stands now, in pixels.</summary>
    public ITexture Motion { get; }

    /// <summary>The targets in the order of <see cref="Formats"/>.</summary>
    public IReadOnlyList<ITexture> Targets { get; }

    public void Dispose()
    {
        foreach (ITexture target in Targets)
        {
            target.Dispose();
        }
    }

    private static ITexture Create(IGraphicsDevice device, int width, int height, PixelFormat format, string name)
    {
        TextureDescription description = new(width, height, format, TextureUsage.RenderTarget | TextureUsage.ShaderRead | TextureUsage.CopySource) { Name = name };
        return device.CreateTexture(description);
    }
}
