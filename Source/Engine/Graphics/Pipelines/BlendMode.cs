namespace FlickerVoid.Engine.Graphics.Pipelines;

/// <summary>How a pixel shader's output combines with the render target.</summary>
public enum BlendMode : byte
{
    Opaque,

    /// <summary>Straight alpha: source * alpha + destination * (1 - alpha).</summary>
    AlphaBlend,

    /// <summary>Premultiplied alpha: source + destination * (1 - alpha).</summary>
    Premultiplied,
    Additive,

    /// <summary>Source * destination, like a glaze over what is already there.</summary>
    Multiply,
}

