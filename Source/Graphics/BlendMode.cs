namespace FlickerVoid.Engine.Graphics;

/// <summary>How a pixel shader's output combines with the render target.</summary>
public enum BlendMode : byte
{
    Opaque,

    /// <summary>Straight alpha: source * alpha + destination * (1 - alpha).</summary>
    AlphaBlend,

    /// <summary>Premultiplied alpha: source + destination * (1 - alpha).</summary>
    Premultiplied,
    Additive,
}
