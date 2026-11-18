namespace FlickerVoid.Engine.Graphics;

/// <summary>A 2D GPU texture.</summary>
public interface ITexture : IGraphicsResource
{
    TextureDescription Description { get; }
}
