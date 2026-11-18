namespace FlickerVoid.Engine.Graphics;

/// <summary>A sampler shaders reach through its bindless index.</summary>
public interface ISampler : IGraphicsResource
{
    SamplerDescription Description { get; }
}
