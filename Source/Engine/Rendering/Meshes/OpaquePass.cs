using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws the lit scene into the HDR target over the prepass depth.</summary>
internal sealed class OpaquePass : IRenderPass
{
    /// <summary>Transparent where nothing is drawn, so post-processing passes coverage on (a stage shows over the interface there).</summary>
    private static readonly Vector4 Background = new(0, 0, 0, 0);

    private readonly IPipeline _pipeline;

    public OpaquePass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _pipeline = MeshDrawing.CreatePipeline(device, shaders, "PixelMain", [ScreenTargets.HdrFormat], "Scene");
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        commands.Barrier(targets.Depth, ResourceState.DepthWrite);
        commands.Barrier(targets.Hdr, ResourceState.RenderTarget);
        ColorTarget hdr = new(targets.Hdr, LoadAction.Clear, Background);
        DepthTarget depth = new(targets.Depth, LoadAction.Load);
        commands.BeginRendering([hdr], depth);
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
