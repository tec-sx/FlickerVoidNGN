using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>
/// Fills what the scene left empty in the lit scene with the mood's sky, before the depth fade, the upscale and the grade, while
/// the renderer's <see cref="SceneRenderer.Sky"/> is on: a gradient from the ground through the horizon to the zenith, which looks
/// on screen as its colours were picked whatever the mood's grade (over a mood's canvas a thin wash the paper shows through), and
/// over it the sun's and moon's discs as light, which bloom.
/// </summary>
public sealed class SkyPass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IPipeline _pipeline;
    private readonly FrameBufferRing _data;

    public SkyPass(IGraphicsDevice device, ShaderLibrary shaders, int framesInFlight)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("Sky", "ComputeMain"), Name = "Sky" };
        _pipeline = device.CreateComputePipeline(description);
        _data = new FrameBufferRing(device, framesInFlight);
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

        GpuSky sky = GpuSky.From(context.Mood);
        _data.Advance();
        IBuffer data = _data.Get(Marshal.SizeOf<GpuSky>());
        data.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<GpuSky>(in sky)));
        Grading grade = Grading.From(context.Mood);
        SkyConstants constants = new(grade, context.Canvas.File.Tint, canvas, context.FrameBuffer, targets.Hdr.BindlessIndex, context.Sampler.BindlessIndex, orthographic, data.BindlessIndex);
        commands.PushConstants(constants);
        commands.Dispatch((targets.Width + GroupSize - 1) / GroupSize, (targets.Height + GroupSize - 1) / GroupSize);
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _data.Dispose();
    }
}
