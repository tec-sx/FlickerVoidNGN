using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Copies one of the G-buffer's targets into a readback buffer once the passes before it drew the frame.</summary>
internal sealed class GBufferReadbackPass(Func<GBuffer, ITexture> target, IBuffer readback) : IRenderPass
{
    /// <summary>The format of the target it copied, known once it ran.</summary>
    public PixelFormat Format { get; private set; }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ITexture texture = target(context.GBuffer);
        Format = texture.Description.Format;
        context.Commands.Barrier(texture, ResourceState.CopySource);
        context.Commands.CopyTextureToBuffer(texture, readback, 0);
    }

    public void Dispose()
    {
    }
}
