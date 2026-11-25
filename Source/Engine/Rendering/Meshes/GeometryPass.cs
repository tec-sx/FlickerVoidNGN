using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Draws the meshes the view sees into the scene's depth and the <see cref="GBuffer"/>, unlit, cutting away what the cutaway
/// does, for screen shadows and lighting to read. Each draw reads its record in the frame's data buffer by its number.
/// </summary>
internal sealed class GeometryPass : IRenderPass
{
    private readonly IPipeline _pipeline;
    private readonly ColorTarget[] _targets = new ColorTarget[GBuffer.Formats.Count];

    public GeometryPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("Scene", "VertexMain"),
            PixelShader = shaders.Load("Scene", "GeometryMain"),
            ColorFormats = GBuffer.Formats,
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = "Geometry",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
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

        ITexture depthTarget = context.Targets.Depth;
        commands.Barrier(depthTarget, ResourceState.DepthWrite);
        DepthTarget clearDepth = new(depthTarget, LoadAction.Clear);
        commands.BeginRendering(_targets, clearDepth);
        commands.SetViewport(new Viewport(0, 0, context.RenderWidth, context.RenderHeight));
        commands.SetScissor(0, 0, context.RenderWidth, context.RenderHeight);
        commands.SetPipeline(_pipeline);
        DrawMeshes(context);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }

    private static void DrawMeshes(RenderContext context)
    {
        ICommandList commands = context.Commands;
        for (int index = 0; index < context.Draws.Count; index++)
        {
            MeshDraw draw = context.Draws[index];
            if (!draw.InView)
            {
                continue;
            }

            GpuMeshPart part = draw.Part;
            SceneConstants constants = new(context.FrameBuffer, (uint)index);
            commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(part.IndexCount);
        }
    }
}
