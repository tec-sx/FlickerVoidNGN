using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Hosting;

/// <summary>Several overlays drawn as one, each over the ones before it, such as the debug interface over the game's.</summary>
public sealed class FrameOverlays(IEnumerable<IFrameOverlay> overlays) : IFrameOverlay
{
    private readonly IFrameOverlay[] _overlays = [.. overlays];

    public void Update(float deltaSeconds)
    {
        foreach (IFrameOverlay overlay in _overlays)
        {
            overlay.Update(deltaSeconds);
        }
    }

    public void Draw(ICommandList commands, ITexture target)
    {
        foreach (IFrameOverlay overlay in _overlays)
        {
            overlay.Draw(commands, target);
        }
    }

    public void Dispose()
    {
        foreach (IFrameOverlay overlay in _overlays)
        {
            overlay.Dispose();
        }
    }
}
