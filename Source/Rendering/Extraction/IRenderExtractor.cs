using Arch.Core;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>Copies what a frame draws out of a game world into a render scene.</summary>
public interface IRenderExtractor
{
    /// <summary>Clears the scene and fills it from the world.</summary>
    void Extract(World world, RenderScene scene);
}
