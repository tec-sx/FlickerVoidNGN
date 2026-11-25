using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws the meshes of the frame its view draws with the pipeline already set, each from its record in the frame's data buffer.</summary>
internal static class MeshDrawing
{
    public static void Draw(RenderContext context)
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

    /// <summary>The scene shader's vertex stage with the given pixel stage, drawing into the given color formats and the scene depth.</summary>
    public static IPipeline CreatePipeline(IGraphicsDevice device, ShaderLibrary shaders, string pixelEntryPoint, IReadOnlyList<PixelFormat> colorFormats, string name)
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
