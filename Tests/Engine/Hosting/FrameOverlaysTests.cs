using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Hosting.Tests;

public sealed class FrameOverlaysTests
{
    private readonly FrameOverlaysTestsBuilder _builder = new();

    [Fact]
    public void UpdatesAndDrawsEachOverlayInOrder()
    {
        FrameOverlays overlays = _builder.WithOverlays(2).Build();
        ICommandList commands = Substitute.For<ICommandList>();
        ITexture target = Substitute.For<ITexture>();
        IFrameOverlay first = _builder.Overlays[0];
        IFrameOverlay second = _builder.Overlays[1];

        overlays.Update(0.5f);
        overlays.Draw(commands, target);

        Received.InOrder(() =>
        {
            first.Update(0.5f);
            second.Update(0.5f);
            first.Draw(commands, target);
            second.Draw(commands, target);
        });
    }

    [Fact]
    public void DisposesEveryOverlay()
    {
        FrameOverlays overlays = _builder.WithOverlays(2).Build();

        overlays.Dispose();

        _builder.Overlays[0].Received(1).Dispose();
        _builder.Overlays[1].Received(1).Dispose();
    }
}
