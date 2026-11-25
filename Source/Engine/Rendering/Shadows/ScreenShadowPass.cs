using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// Works out contact shadows and ambient occlusion from the scene depth and normals into the screen shadows target, unless the
/// frame traces rays, which trace the occlusion and sun shadows that need no contact shadow (<see cref="ShadowRayPass"/>).
/// </summary>
internal sealed class ScreenShadowPass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IPipeline _pipeline;

    public ScreenShadowPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        ComputePipelineDescription description = new()
        {
            ComputeShader = shaders.Load("ScreenShadows", "ComputeMain"),
            Name = "Screen shadows",
        };
        _pipeline = device.CreateComputePipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (context.RayOcclusion is not null)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(context.GBuffer.Normal, ResourceState.ShaderRead);
        commands.Barrier(targets.ScreenShadows, ResourceState.ShaderWrite);
        commands.SetPipeline(_pipeline);
        ScreenShadowConstants constants = new(context.FrameBuffer, targets.ScreenShadows.BindlessIndex);
        commands.PushConstants(constants);
        commands.Dispatch((context.Width + GroupSize - 1) / GroupSize, (context.Height + GroupSize - 1) / GroupSize);
        commands.Barrier(targets.ScreenShadows, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
