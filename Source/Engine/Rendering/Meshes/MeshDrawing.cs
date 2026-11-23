using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws every mesh of the frame but shadow-only ones with the pipeline already set, reading the frame's data buffer.</summary>
internal static class MeshDrawing
{
    public static void Draw(RenderContext context)
    {
        ICommandList commands = context.Commands;
        foreach (MeshDraw draw in context.Draws)
        {
            if ((draw.Flags & (MeshFlags.ShadowOnly | MeshFlags.OutlineOnly)) != 0)
            {
                continue;
            }

            GpuMeshPart part = draw.Part;
            ITexture texture = part.Texture ?? context.White;
            SceneConstants constants = new(draw.World, part.BaseColor, context.FrameBuffer, draw.Vertices.BindlessIndex, texture.BindlessIndex, context.Sampler.BindlessIndex, draw.Material.BindlessIndex, (uint)draw.Flags);
            commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(part.IndexCount);
        }
    }

    /// <summary>The scene shader's vertex stage with the given pixel stage, drawing into the given color formats and the scene depth.</summary>
    public static IPipeline CreatePipeline(IGraphicsDevice device, ShaderLibrary shaders, string pixelEntryPoint, PixelFormat[] colorFormats, string name)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = shaders.Load("Scene", "VertexMain"),
            PixelShader = shaders.Load("Scene", pixelEntryPoint),
            ColorFormats = colorFormats,
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = name,
        };
        return device.CreateGraphicsPipeline(description);
    }
}
