namespace FlickerVoid.Engine.Graphics;

/// <summary>How a sampler reads outside 0..1.</summary>
public enum SamplerAddressMode : byte
{
    Wrap,
    Mirror,
    Clamp,
}
