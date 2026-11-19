using System.Numerics;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Blurs the lit scene into bloom and grades it into the output target, as the mood says.</summary>
internal sealed class PostProcessor : IDisposable
{
    private const string BloomShader = "Bloom.hlsl";
    private const string PostShader = "Post.hlsl";
    private const int GroupSize = 8;

    private readonly IPipeline _down;
    private readonly IPipeline _up;
    private readonly IPipeline _composite;

    public PostProcessor(IGraphicsDevice device, IShaderCompiler compiler, PixelFormat outputFormat)
    {
        ComputePipelineDescription down = new() { ComputeShader = EmbeddedShaders.Compile(compiler, BloomShader, "DownMain", ShaderStage.Compute), Name = "Bloom down" };
        ComputePipelineDescription up = new() { ComputeShader = EmbeddedShaders.Compile(compiler, BloomShader, "UpMain", ShaderStage.Compute), Name = "Bloom up" };
        _down = device.CreateComputePipeline(down);
        _up = device.CreateComputePipeline(up);
        GraphicsPipelineDescription composite = new()
        {
            VertexShader = EmbeddedShaders.Compile(compiler, PostShader, "VertexMain", ShaderStage.Vertex),
            PixelShader = EmbeddedShaders.Compile(compiler, PostShader, "PixelMain", ShaderStage.Pixel),
            ColorFormats = [outputFormat],
            CullMode = CullMode.None,
            Name = "Post",
        };
        _composite = device.CreateGraphicsPipeline(composite);
    }

    /// <summary>Leaves the output in the RenderTarget state.</summary>
    public void Run(ICommandList commands, ScreenTargets targets, ITexture output, LightingMood mood, ITexture? paper, ISampler clamp, ISampler wrap)
    {
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
        uint bloom = GpuLight.None;
        if (mood.Bloom > 0)
        {
            DrawBloom(commands, targets, clamp);
            bloom = targets.Bloom[0].BindlessIndex;
        }

        uint paperIndex = GpuLight.None;
        if (paper is not null)
        {
            paperIndex = paper.BindlessIndex;
        }

        commands.Barrier(output, ResourceState.RenderTarget);
        ColorTarget color = new(output, LoadAction.DontCare);
        commands.BeginRendering([color]);
        int width = output.Description.Width;
        int height = output.Description.Height;
        commands.SetViewport(new Viewport(0, 0, width, height));
        commands.SetScissor(0, 0, width, height);
        commands.SetPipeline(_composite);
        PostConstants constants = new(
            mood.Tint,
            mood.Exposure,
            mood.Contrast,
            mood.Saturation,
            mood.Bloom,
            mood.Grain,
            targets.Hdr.BindlessIndex,
            bloom,
            paperIndex,
            clamp.BindlessIndex,
            wrap.BindlessIndex);
        commands.PushConstants(constants);
        commands.Draw(3);
        commands.EndRendering();
    }

    public void Dispose()
    {
        _composite.Dispose();
        _up.Dispose();
        _down.Dispose();
    }

    private void DrawBloom(ICommandList commands, ScreenTargets targets, ISampler clamp)
    {
        ITexture[] levels = targets.Bloom;
        commands.SetPipeline(_down);
        ITexture source = targets.Hdr;
        foreach (ITexture level in levels)
        {
            commands.Barrier(level, ResourceState.ShaderWrite);
            Dispatch(commands, source, level, clamp);
            commands.Barrier(level, ResourceState.ShaderRead);
            source = level;
        }

        commands.SetPipeline(_up);
        for (int index = levels.Length - 2; index >= 0; index--)
        {
            ITexture destination = levels[index];
            commands.Barrier(destination, ResourceState.ShaderWrite);
            Dispatch(commands, levels[index + 1], destination, clamp);
            commands.Barrier(destination, ResourceState.ShaderRead);
        }
    }

    private static void Dispatch(ICommandList commands, ITexture source, ITexture destination, ISampler clamp)
    {
        Vector2 sourceTexel = new(1f / source.Description.Width, 1f / source.Description.Height);
        Vector2 destinationSize = new(destination.Description.Width, destination.Description.Height);
        BloomConstants constants = new(sourceTexel, destinationSize, source.BindlessIndex, destination.BindlessIndex, clamp.BindlessIndex);
        commands.PushConstants(constants);
        int groupsX = (destination.Description.Width + GroupSize - 1) / GroupSize;
        int groupsY = (destination.Description.Height + GroupSize - 1) / GroupSize;
        commands.Dispatch(groupsX, groupsY);
    }
}
