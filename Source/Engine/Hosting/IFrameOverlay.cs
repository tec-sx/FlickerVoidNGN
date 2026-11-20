using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Hosting;

/// <summary>Something drawn over the rendered scene in the window each frame, such as the debug interface.</summary>
public interface IFrameOverlay : IDisposable
{
    void Update(float deltaSeconds);

    void Draw(ICommandList commands, ITexture target);
}
