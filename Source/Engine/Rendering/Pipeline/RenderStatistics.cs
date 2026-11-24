namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>How many mesh parts the last frame drew, and skipped as wholly outside, in its view and summed over its shadow views.</summary>
public sealed class RenderStatistics
{
    public int Drawn { get; internal set; }

    public int Culled { get; internal set; }

    public int ShadowDrawn { get; internal set; }

    public int ShadowCulled { get; internal set; }

    internal void Reset()
    {
        Drawn = 0;
        Culled = 0;
        ShadowDrawn = 0;
        ShadowCulled = 0;
    }
}
