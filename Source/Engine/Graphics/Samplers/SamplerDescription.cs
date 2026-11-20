namespace FlickerVoid.Engine.Graphics.Samplers;

/// <summary>Filtering and addressing of a sampler.</summary>
public readonly record struct SamplerDescription(SamplerFilter Filter, SamplerAddressMode AddressMode);

