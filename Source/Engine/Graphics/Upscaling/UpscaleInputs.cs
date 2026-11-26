using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Upscaling;

/// <summary>
/// What one frame gives an upscaler: its lit color (HDR, before exposure), depth (0 near, 1 far) and motion at the render size,
/// the output it fills, the jitter it was drawn with, the exposure its color is graded with and whether to drop the frames
/// before. Motion is in render pixels, from each pixel to where its surface stood the frame before, without the jitter; the
/// jitter is how far the view was moved, in render pixels right and down, from -0.5 to 0.5.
/// </summary>
public readonly record struct UpscaleInputs(ITexture Color, ITexture Depth, ITexture Motion, ITexture Output, Vector2 Jitter, float Exposure, bool Reset)
{
    /// <summary>What an upscaler that denoises reads beside the frame, in the ShaderRead state; null for one that doesn't.</summary>
    public DenoiseGuides? Guides { get; init; }
}
