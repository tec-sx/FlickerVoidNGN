namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// How many mesh parts the last frame drew, and skipped as wholly outside, in its view and summed over its shadow views, how many
/// instance batches and copies, and the size it was drawn at for the output's.
/// </summary>
public sealed class RenderStatistics
{
    public int Drawn { get; internal set; }

    public int Culled { get; internal set; }

    public int ShadowDrawn { get; internal set; }

    public int ShadowCulled { get; internal set; }

    /// <summary>Instance batches drawn in the view, and skipped as wholly outside it.</summary>
    public int BatchesDrawn { get; internal set; }

    public int BatchesCulled { get; internal set; }

    /// <summary>The copies of the batches in view, which the GPU then culls one by one.</summary>
    public int CopiesSubmitted { get; internal set; }

    /// <summary>
    /// The copies the GPU kept after culling them one by one, read back from the last frame drawn in this frame's slot, so
    /// <see cref="SceneRenderer.MaxFramesInFlight"/> frames late.
    /// </summary>
    public int CopiesDrawn { get; internal set; }

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
        BatchesDrawn = 0;
        BatchesCulled = 0;
        CopiesSubmitted = 0;
    }
}
