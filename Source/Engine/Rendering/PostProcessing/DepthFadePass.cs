using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// Lays the camera's depth fade (<see cref="RenderContext.DepthFade"/>) on the lit scene before the upscale and the grade: what
/// stands nearer than the focus darkens and what lies farther melts into the far colour, as it shows on screen.
/// </summary>
internal sealed class DepthFadePass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IPipeline _pipeline;

    public DepthFadePass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("DepthFade", "ComputeMain"), Name = "Depth fade" };
        _pipeline = device.CreateComputePipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (!context.DepthFade.IsVisible)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(targets.Hdr, ResourceState.ShaderWrite);
        commands.SetPipeline(_pipeline);
        Grading grade = Grading.From(context.Mood);
        DepthFadeConstants constants = new(grade, context.FrameBuffer, targets.Hdr.BindlessIndex);
        commands.PushConstants(constants);
        commands.Dispatch((targets.Width + GroupSize - 1) / GroupSize, (targets.Height + GroupSize - 1) / GroupSize);
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
