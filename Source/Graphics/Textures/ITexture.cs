using FlickerVoid.Engine.Graphics.Resources;

namespace FlickerVoid.Engine.Graphics.Textures;

/// <summary>A 2D GPU texture.</summary>
public interface ITexture : IGraphicsResource
{
    TextureDescription Description { get; }
}

