namespace FlickerVoid.Engine.Graphics.Upscaling;

/// <summary>What an upscaler is made for: its mode and the size of the output it fills; the device picks the size frames are drawn at.</summary>
public readonly record struct UpscalerDescription(UpscaleMode Mode, int OutputWidth, int OutputHeight)
{
    /// <summary>
    /// Whether it also denoises the frames' ray traced light, as DLSS Ray Reconstruction does, in place of filters of the
    /// renderer's own: only on devices with <see cref="Devices.GraphicsFeatures.RayReconstruction"/>, and every frame then gives
    /// it <see cref="UpscaleInputs.Guides"/>.
    /// </summary>
    public bool Denoises { get; init; }
}
