using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>
/// Fills what the scene left empty in the lit scene with a sky-like gradient, lighter above the horizon, before the depth fade,
/// the upscale and the grade, while the renderer's <see cref="SceneRenderer.Sky"/> is on. It looks on screen as its colours were
/// picked whatever the mood's grade, and over a mood's canvas it is a thin wash the paper shows through.
/// </summary>
public sealed class SkyPass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IPipeline _pipeline;

    public SkyPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("Sky", "ComputeMain"), Name = "Sky" };
        _pipeline = device.CreateComputePipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (!context.Sky)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(targets.Hdr, ResourceState.ShaderWrite);
        commands.SetPipeline(_pipeline);
        uint canvas = GpuLight.None;
        if (context.Canvas.Image is { } image)
        {
            canvas = image.BindlessIndex;
        }

        float orthographic = 0;
        if (context.View.Projection.M34 == 0)
        {
            orthographic = 1;
        }

        Grading grade = Grading.From(context.Mood);
        SkyConstants constants = new(grade, context.Canvas.File.Tint, canvas, context.FrameBuffer, targets.Hdr.BindlessIndex, context.Sampler.BindlessIndex, orthographic);
        commands.PushConstants(constants);
        commands.Dispatch((targets.Width + GroupSize - 1) / GroupSize, (targets.Height + GroupSize - 1) / GroupSize);
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
