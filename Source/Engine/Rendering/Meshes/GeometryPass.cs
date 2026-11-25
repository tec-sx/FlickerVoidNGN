using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Draws the meshes the view sees into the scene's depth and the <see cref="GBuffer"/>, unlit, cutting away what the cutaway
/// does, for screen shadows and lighting to read.
/// </summary>
internal sealed class GeometryPass : IRenderPass
{
    private readonly IPipeline _pipeline;
    private readonly ColorTarget[] _targets = new ColorTarget[GBuffer.Formats.Count];

    public GeometryPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _pipeline = MeshDrawing.CreatePipeline(device, shaders, "GeometryMain", GBuffer.Formats, "Geometry");
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ICommandList commands = context.Commands;
        IReadOnlyList<ITexture> gBuffer = context.GBuffer.Targets;
        for (int index = 0; index < _targets.Length; index++)
        {
            ITexture target = gBuffer[index];
            commands.Barrier(target, ResourceState.RenderTarget);
            _targets[index] = new ColorTarget(target, LoadAction.Clear);
        }

        ITexture depthTarget = context.Depth;
        commands.Barrier(depthTarget, ResourceState.DepthWrite);
        DepthTarget clearDepth = new(depthTarget, LoadAction.Clear);
        commands.BeginRendering(_targets, clearDepth);
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
