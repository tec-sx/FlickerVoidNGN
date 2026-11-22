using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>
/// Draws the outlines of meshes with an <see cref="Outline"/> over the output, like a custom depth stencil: their silhouettes in
/// their colors into a mask, then a line <see cref="Width"/> pixels wide around them.
/// </summary>
public sealed class OutlinePass : IRenderPass
{
    private const PixelFormat MaskFormat = PixelFormat.Rgba8UNorm;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _silhouettes;
    private readonly IPipeline _line;
    private ITexture? _mask;

    internal OutlinePass(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat outputFormat)
    {
        _device = device;
        GraphicsPipelineDescription silhouettes = new()
        {
            VertexShader = shaders.Load("Scene", "VertexMain"),
            PixelShader = shaders.Load("Scene", "OutlineMain"),
            ColorFormats = [MaskFormat],
            CullMode = CullMode.None,
            Name = "Outline silhouettes",
        };
        _silhouettes = device.CreateGraphicsPipeline(silhouettes);
        GraphicsPipelineDescription line = new()
        {
            VertexShader = shaders.Load("Outline", "VertexMain"),
            PixelShader = shaders.Load("Outline", "PixelMain"),
            ColorFormats = [outputFormat],
            CullMode = CullMode.None,
            Name = "Outline",
        };
        _line = device.CreateGraphicsPipeline(line);
    }

    /// <summary>How far the line reaches from a silhouette, in pixels.</summary>
    public int Width { get; set; } = 2;

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (!HasOutlines(context))
        {
            return;
        }

        ITexture mask = EnsureMask(context.Width, context.Height);
        ICommandList commands = context.Commands;
        commands.Barrier(mask, ResourceState.RenderTarget);
        ColorTarget clear = new(mask, LoadAction.Clear, Vector4.Zero);
        commands.BeginRendering([clear]);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        commands.SetPipeline(_silhouettes);
        DrawSilhouettes(context);
        commands.EndRendering();

        commands.Barrier(mask, ResourceState.ShaderRead);
        commands.Barrier(context.ColorTarget, ResourceState.RenderTarget);
        ColorTarget output = new(context.ColorTarget);
        commands.BeginRendering([output]);
        commands.SetViewport(new Viewport(0, 0, context.Width, context.Height));
        commands.SetScissor(0, 0, context.Width, context.Height);
        commands.SetPipeline(_line);
        OutlineConstants constants = new(mask.BindlessIndex, (uint)Width);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _mask?.Dispose();
        _line.Dispose();
        _silhouettes.Dispose();
    }

    private static bool HasOutlines(RenderContext context)
    {
        foreach (MeshDraw draw in context.Draws)
        {
            if (draw.Outline is not null)
            {
                return true;
            }
        }

        return false;
    }

    // The silhouette carries the outline's color, so the line can take the color of the mesh it surrounds.
    private static void DrawSilhouettes(RenderContext context)
    {
        ICommandList commands = context.Commands;
        foreach (MeshDraw draw in context.Draws)
        {
            if (draw.Outline is not { } outline)
            {
                continue;
            }

            GpuMeshPart part = draw.Part;
            Vector4 color = new(outline.Color, 1);
            SceneConstants constants = new(draw.World, color, context.FrameBuffer, draw.Vertices.BindlessIndex, context.White.BindlessIndex, context.Sampler.BindlessIndex, draw.Material.BindlessIndex);
            commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(part.IndexCount);
        }
    }

    /// <summary>Remakes the mask when the size changes; the device releases the old one once the GPU is done with it.</summary>
    private ITexture EnsureMask(int width, int height)
    {
        if (_mask is not null && _mask.Description.Width == width && _mask.Description.Height == height)
        {
            return _mask;
        }

        _mask?.Dispose();
        TextureDescription description = new(width, height, MaskFormat, TextureUsage.RenderTarget | TextureUsage.ShaderRead) { Name = "Outline mask" };
        _mask = _device.CreateTexture(description);
        return _mask;
    }
}
