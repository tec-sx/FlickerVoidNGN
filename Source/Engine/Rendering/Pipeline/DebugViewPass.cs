using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// Draws one of the G-buffer's targets or the depth over the output in place of the lit frame, black where nothing was drawn,
/// while the renderer's <see cref="SceneRenderer.DebugView"/> asks for one; outlines and the editor's overlays still draw over it.
/// </summary>
internal sealed class DebugViewPass : IRenderPass
{
    private readonly IPipeline _pipeline;

    public DebugViewPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat colorFormat)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("DebugView", "VertexMain"),
            PixelShader = shaders.Load("DebugView", "PixelMain"),
            ColorFormats = [colorFormat],
            CullMode = CullMode.None,
            Name = "Debug view",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (context.DebugView == DebugView.Lit)
        {
            return;
        }

        ICommandList commands = context.Commands;
        IReadOnlyList<ITexture> gBuffer = context.GBuffer.Targets;
        for (int index = 0; index < gBuffer.Count; index++)
        {
            commands.Barrier(gBuffer[index], ResourceState.ShaderRead);
        }

        commands.Barrier(context.Depth, ResourceState.ShaderRead);
        commands.Barrier(context.ColorTarget, ResourceState.RenderTarget);
        ColorTarget color = new(context.ColorTarget, LoadAction.Load);
        commands.BeginRendering([color]);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        commands.SetPipeline(_pipeline);
        DebugViewConstants constants = new(context.FrameBuffer, (uint)context.DebugView);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
