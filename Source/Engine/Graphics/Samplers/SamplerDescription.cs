namespace FlickerVoid.Engine.Graphics.Samplers;

/// <summary>Filtering and addressing of a sampler.</summary>
public readonly record struct SamplerDescription(SamplerFilter Filter, SamplerAddressMode AddressMode)
{
    /// <summary>Added to the mip level textures are sampled at; below 0 samples sharper levels, as frames drawn for an upscaler want.</summary>
    public float MipBias { get; init; }
}

