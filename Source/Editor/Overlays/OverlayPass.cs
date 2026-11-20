using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Editor.Overlays;

/// <summary>
/// Draws the overlay's triangles over the finished image, in front of everything; add it last. Each frame's vertices go to the
/// buffer of that frame's slot, grown when they don't fit.
/// </summary>
public sealed class OverlayPass : IRenderPass
{
    private const int FramesInFlight = SceneRenderer.MaxFramesInFlight;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _pipeline;
    private readonly IBuffer?[] _buffers = new IBuffer?[FramesInFlight];
    private int _slot;

    public OverlayPass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat colorFormat)
    {
        _device = device;
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("Overlay", "VertexMain"),
            PixelShader = shaders.Load("Overlay", "PixelMain"),
            ColorFormats = [colorFormat],
            CullMode = CullMode.None,
            Blend = BlendMode.AlphaBlend,
            Name = "Overlay",
        };
        _pipeline = device.CreateGraphicsPipeline(description);
    }

    /// <summary>What the next frames draw; fill it before rendering.</summary>
    public OverlayBuilder Overlay { get; } = new();

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ReadOnlySpan<OverlayVertex> vertices = Overlay.Vertices;
        if (vertices.Length == 0)
        {
            return;
        }

        _slot = (_slot + 1) % FramesInFlight;
        IBuffer buffer = Upload(vertices);
        ICommandList commands = context.Commands;
        commands.Barrier(context.ColorTarget, ResourceState.RenderTarget);
        ColorTarget color = new(context.ColorTarget, LoadAction.Load);
        commands.BeginRendering([color]);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        commands.SetPipeline(_pipeline);
        Matrix4x4 viewProjection = context.View.ViewProjection;
        OverlayConstants constants = new(viewProjection, buffer.BindlessIndex);
        commands.PushConstants(constants);
        commands.Draw(vertices.Length);
        commands.EndRendering();
    }

    public void Dispose()
    {
        foreach (IBuffer? buffer in _buffers)
        {
            buffer?.Dispose();
        }

        _pipeline.Dispose();
    }

    private IBuffer Upload(ReadOnlySpan<OverlayVertex> vertices)
    {
        long size = (long)vertices.Length * OverlayVertex.Size;
        IBuffer? buffer = _buffers[_slot];
        if (buffer is null || buffer.Description.Size < size)
        {
            // The device releases a disposed buffer only after the GPU finishes the work already submitted, which may still read it.
            buffer?.Dispose();
            long capacity = (long)BitOperations.RoundUpToPowerOf2((ulong)size);
            BufferDescription description = new(capacity, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = $"Overlay {_slot}" };
            buffer = _device.CreateBuffer(description);
            _buffers[_slot] = buffer;
        }

        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(vertices);
        buffer.Write(bytes, 0);
        return buffer;
    }
}
