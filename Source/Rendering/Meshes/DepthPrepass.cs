using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws the scene's depth alone, for the screen shadows and ambient occlusion to read before the lit pass.</summary>
internal sealed class DepthPrepass : IRenderPass
{
    private readonly IPipeline _pipeline;

    public DepthPrepass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _pipeline = MeshDrawing.CreatePipeline(device, shaders, "DepthMain", [], "Depth prepass");
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ICommandList commands = context.Commands;
        ITexture depthTarget = context.Targets.Depth;
        commands.Barrier(depthTarget, ResourceState.DepthWrite);
        DepthTarget clearDepth = new(depthTarget, LoadAction.Clear);
        commands.BeginRendering([], clearDepth);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        commands.SetPipeline(_pipeline);
        MeshDrawing.Draw(context);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
