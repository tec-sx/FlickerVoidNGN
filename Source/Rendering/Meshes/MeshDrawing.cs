using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws every mesh of the frame with the pipeline already set, reading the frame's data buffer.</summary>
internal static class MeshDrawing
{
    public static void Draw(RenderContext context)
    {
        ICommandList commands = context.Commands;
        foreach (MeshDraw draw in context.Draws)
        {
            GpuMeshPart part = draw.Part;
            ITexture texture = part.Texture ?? context.White;
            SceneConstants constants = new(draw.World, part.BaseColor, context.FrameBuffer, draw.Vertices.BindlessIndex, texture.BindlessIndex, context.Sampler.BindlessIndex);
            commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(part.IndexCount);
        }
    }

    /// <summary>The scene shader's vertex stage with the given pixel stage, drawing into the given color formats and the scene depth.</summary>
    public static IPipeline CreatePipeline(IGraphicsDevice device, IShaderCompiler compiler, string pixelEntryPoint, PixelFormat[] colorFormats, string name)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = EmbeddedShaders.Compile(compiler, "Scene.hlsl", "VertexMain", ShaderStage.Vertex, "Frame.hlsl"),
            PixelShader = EmbeddedShaders.Compile(compiler, "Scene.hlsl", pixelEntryPoint, ShaderStage.Pixel, "Frame.hlsl"),
            ColorFormats = colorFormats,
            DepthFormat = SceneRenderer.DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = name,
        };
        return device.CreateGraphicsPipeline(description);
    }
}
