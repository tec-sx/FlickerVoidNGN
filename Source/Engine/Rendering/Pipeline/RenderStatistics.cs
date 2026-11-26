namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// How many mesh parts the last frame drew, and skipped as wholly outside, in its view and summed over its shadow views, and the
/// size it was drawn at for the output's.
/// </summary>
public sealed class RenderStatistics
{
    public int Drawn { get; internal set; }

    public int Culled { get; internal set; }

    public int ShadowDrawn { get; internal set; }

    public int ShadowCulled { get; internal set; }

    /// <summary>The width the scene was drawn at: the output's, or an upscaler's smaller one.</summary>
    public int RenderWidth { get; internal set; }

    public int RenderHeight { get; internal set; }

    public int OutputWidth { get; internal set; }

    public int OutputHeight { get; internal set; }

    /// <summary>Whether an upscaler filled the output in from the scene.</summary>
    public bool Upscaled { get; internal set; }

    /// <summary>Whether the upscaler also denoised the scene's ray traced light, with DLSS Ray Reconstruction.</summary>
    public bool Denoised { get; internal set; }

    internal void Reset()
    {
        Drawn = 0;
        Culled = 0;
        ShadowDrawn = 0;
        ShadowCulled = 0;
    }
}
