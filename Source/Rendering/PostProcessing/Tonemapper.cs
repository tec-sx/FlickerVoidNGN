namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// The curve that brings the graded scene's unbounded light into the screen's range. A new one is a value here and a case
/// in Post.hlsl's Tonemap.
/// </summary>
public enum Tonemapper
{
    /// <summary>Leaves the light as it is, clipping what is brighter than white.</summary>
    None,

    /// <summary>John Hable's filmic curve from Uncharted 2: a soft toe and a long shoulder that turns bright colors pastel.</summary>
    Uncharted2,

    /// <summary>Krzysztof Narkowicz's fit of the ACES film curve: more contrast and saturation than Uncharted 2.</summary>
    Aces,
}
