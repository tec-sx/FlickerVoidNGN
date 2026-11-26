using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>Blurs the lit scene into bloom and grades it into the output target over the mood's paper, as the mood says.</summary>
internal sealed class PostProcessor : IDisposable
{
    private const string BloomShader = "Bloom";
    private const string PostShader = "Post";
    private const int GroupSize = 8;

    private readonly IPipeline _down;
    private readonly IPipeline _up;
    private readonly IPipeline _composite;

    public PostProcessor(IGraphicsDevice device, ShaderLibrary shaders, PixelFormat outputFormat)
    {
        ComputePipelineDescription down = new() { ComputeShader = shaders.Load(BloomShader, "DownMain"), Name = "Bloom down" };
        ComputePipelineDescription up = new() { ComputeShader = shaders.Load(BloomShader, "UpMain"), Name = "Bloom up" };
        _down = device.CreateComputePipeline(down);
        _up = device.CreateComputePipeline(up);
        GraphicsPipelineDescription composite = new()
        {
            VertexShader = shaders.Load(PostShader, "VertexMain"),
            PixelShader = shaders.Load(PostShader, "PixelMain"),
            ColorFormats = [outputFormat],
            CullMode = CullMode.None,
            Name = "Post",
        };
        _composite = device.CreateGraphicsPipeline(composite);
    }

    /// <summary>Post-processes the lit scene at the output's size (<see cref="ScreenTargets.Lit"/>); leaves the output in the RenderTarget state.</summary>
    public void Run(ICommandList commands, ScreenTargets targets, ITexture output, LightingMood mood, PostCanvas canvas, ITexture? dirt, ISampler clamp, ISampler wrap)
    {
        ITexture lit = targets.Lit;
        commands.Barrier(lit, ResourceState.ShaderRead);
        uint bloom = GpuLight.None;
        if (mood.Bloom > 0 || dirt is not null)
        {
            DrawBloom(commands, targets, clamp, mood.BloomRadius);
            bloom = targets.Bloom[0].BindlessIndex;
        }

        uint canvasIndex = GpuLight.None;
        if (canvas.Image is not null)
        {
            canvasIndex = canvas.Image.BindlessIndex;
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
            Grading.From(mood),
            mood.Bloom,
            canvas.File.Intensity,
            lit.BindlessIndex,
            bloom,
            canvasIndex,
            clamp.BindlessIndex,
            wrap.BindlessIndex,
            canvas.View.ImageHeight,
            canvas.File.Tint,
            canvas.View.PanRight,
            canvas.View.PanUp,
            mood.VignetteIntensity,
            mood.VignetteRadius,
            mood.VignetteSoftness,
            mood.VignetteRoundness,
            dirt is null ? 0 : mood.DirtAmount,
            dirt?.BindlessIndex ?? GpuLight.None,
            0,
            mood.VignetteColor);
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

    private void DrawBloom(ICommandList commands, ScreenTargets targets, ISampler clamp, float radius)
    {
        ITexture[] levels = targets.Bloom;
        commands.SetPipeline(_down);
        ITexture source = targets.Lit;
        foreach (ITexture level in levels)
        {
            commands.Barrier(level, ResourceState.ShaderWrite);
            Dispatch(commands, source, level, clamp, 1);
            commands.Barrier(level, ResourceState.ShaderRead);
            source = level;
        }

        commands.SetPipeline(_up);
        for (int index = levels.Length - 2; index >= 0; index--)
        {
            ITexture destination = levels[index];
            commands.Barrier(destination, ResourceState.ShaderWrite);
            Dispatch(commands, levels[index + 1], destination, clamp, radius);
            commands.Barrier(destination, ResourceState.ShaderRead);
        }
    }

    private static void Dispatch(ICommandList commands, ITexture source, ITexture destination, ISampler clamp, float radius)
    {
        Vector2 sourceTexel = new(1f / source.Description.Width, 1f / source.Description.Height);
        Vector2 destinationSize = new(destination.Description.Width, destination.Description.Height);
        BloomConstants constants = new(sourceTexel, destinationSize, source.BindlessIndex, destination.BindlessIndex, clamp.BindlessIndex, radius);
        commands.PushConstants(constants);
        int groupsX = (destination.Description.Width + GroupSize - 1) / GroupSize;
        int groupsY = (destination.Description.Height + GroupSize - 1) / GroupSize;
        commands.Dispatch(groupsX, groupsY);
    }
}

