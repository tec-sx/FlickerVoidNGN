using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>Lights the G-buffer into the HDR target, a pixel a thread, leaving it transparent where nothing was drawn.</summary>
internal sealed class LightingPass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IPipeline _pipeline;

    public LightingPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        ComputePipelineDescription description = new()
        {
            ComputeShader = shaders.Load("Lighting", "ComputeMain"),
            Name = "Lighting",
        };
        _pipeline = device.CreateComputePipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        ICommandList commands = context.Commands;
        IReadOnlyList<ITexture> gBuffer = context.GBuffer.Targets;
        for (int index = 0; index < gBuffer.Count; index++)
        {
            commands.Barrier(gBuffer[index], ResourceState.ShaderRead);
        }

        ScreenTargets targets = context.Targets;
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(targets.Hdr, ResourceState.ShaderWrite);
        commands.SetPipeline(_pipeline);
        LightingConstants constants = new(context.FrameBuffer, targets.Hdr.BindlessIndex);
        commands.PushConstants(constants);
        commands.Dispatch((context.RenderWidth + GroupSize - 1) / GroupSize, (context.RenderHeight + GroupSize - 1) / GroupSize);
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }
}
