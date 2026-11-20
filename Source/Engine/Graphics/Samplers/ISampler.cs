using FlickerVoid.Engine.Graphics.Resources;

namespace FlickerVoid.Engine.Graphics.Samplers;

/// <summary>A sampler shaders reach through its bindless index.</summary>
public interface ISampler : IGraphicsResource
{
    SamplerDescription Description { get; }
}

