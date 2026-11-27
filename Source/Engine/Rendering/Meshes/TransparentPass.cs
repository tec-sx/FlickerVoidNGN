using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Draws glass over the lit scene, after the lighting and sky passes and before the depth fade and the upscale: the lit scene is
/// copied first (<see cref="ScreenTargets.Behind"/>), for glass to bend and blur what is behind it, then the glass in view is drawn
/// far to near through its master's transparent pass, tested against the scene's depth and writing none. A pane in front of
/// another sees the scene behind both without the one behind it.
/// </summary>
internal sealed class TransparentPass : IRenderPass
{
    private readonly IGraphicsDevice _device;
    private readonly ShaderBytecode _vertexShader;
    private readonly MasterPipelines _masters;
    private readonly List<(float Depth, int Index)> _glass = [];
    private readonly ColorTarget[] _target = new ColorTarget[1];

    public TransparentPass(IGraphicsDevice device, ShaderLibrary shaders, IContentManager content)
    {
        _device = device;
        _vertexShader = shaders.Load("Scene", "VertexMain");
        _masters = new MasterPipelines(content, CreatePipeline);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        CollectGlass(context);
        if (_glass.Count == 0)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        commands.Barrier(targets.Hdr, ResourceState.CopySource);
        commands.Barrier(targets.Behind, ResourceState.CopyDestination);
        commands.CopyTexture(targets.Hdr, targets.Behind);
        commands.Barrier(targets.Behind, ResourceState.ShaderRead);
        commands.Barrier(targets.Hdr, ResourceState.RenderTarget);
        // The attachment layout is the writable one, though the pipelines write no depth.
        commands.Barrier(targets.Depth, ResourceState.DepthWrite);
        _target[0] = new ColorTarget(targets.Hdr);
        DepthTarget depth = new(targets.Depth);
        commands.BeginRendering(_target, depth);
        commands.SetViewport(new Viewport(0, 0, context.RenderWidth, context.RenderHeight));
        commands.SetScissor(0, 0, context.RenderWidth, context.RenderHeight);
        IPipeline? current = null;
        foreach ((_, int index) in _glass)
        {
            MeshDraw draw = context.Draws[index];
            IPipeline pipeline = _masters.Get(draw.Material.Master!);
            if (!ReferenceEquals(pipeline, current))
            {
                commands.SetPipeline(pipeline);
                current = pipeline;
            }

            SceneConstants constants = new(context.FrameBuffer, (uint)index);
            commands.SetIndexBuffer(draw.Part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(draw.Part.IndexCount);
        }

        commands.EndRendering();
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _masters.Dispose();
    }

    /// <summary>The glass in view, farthest first, by how far in front of the camera its box's centre (or its place) stands.</summary>
    private void CollectGlass(RenderContext context)
    {
        _glass.Clear();
        Vector4 depthAxis = context.View.DepthAxis;
        for (int index = 0; index < context.Draws.Count; index++)
        {
            // Batches' copies are never glass.
            MeshDraw draw = context.Draws[index];
            if (!draw.InView || !draw.Material.Transparent || draw.Instanced is not null)
            {
                continue;
            }

            Vector3 center = draw.World.Translation;
            if (draw.Bounds is { } bounds)
            {
                center = bounds.Center;
            }

            float depth = Vector4.Dot(new Vector4(center, 1), depthAxis);
            _glass.Add((depth, index));
        }

        _glass.Sort(static (first, second) => second.Depth.CompareTo(first.Depth));
    }

    private IPipeline CreatePipeline(ShaderBytecode pixelShader, CullMode cull, string master)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = _vertexShader,
            PixelShader = pixelShader,
            CullMode = cull,
            ColorFormats = [ScreenTargets.HdrFormat],
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: false),
            Name = $"Transparent {master}",
        };
        return _device.CreateGraphicsPipeline(description);
    }
}
