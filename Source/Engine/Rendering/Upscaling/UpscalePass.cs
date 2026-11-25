using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Upscaling;

/// <summary>
/// Fills the output's size from the lit scene with the renderer's upscaler, after the depth fade and before post-processing, and
/// carries the scene's alpha (its paint's coverage and thickness) over, which the upscaler leaves out (Shaders/Upscale.hlsl);
/// does nothing while the scene is drawn at the output's size.
/// </summary>
internal sealed class UpscalePass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IPipeline _mask;

    public UpscalePass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("Upscale", "MaskMain"), Name = "Upscale mask" };
        _mask = device.CreateComputePipeline(description);
    }

    public void Prepare(RenderContext context)
    {
    }

    public void Execute(RenderContext context)
    {
        if (context.Upscaler is not { } upscaler)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        ITexture output = targets.Upscaled!;
        ITexture motion = targets.GBuffer.Motion;
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(motion, ResourceState.ShaderRead);
        commands.Barrier(output, ResourceState.ShaderWrite);
        UpscaleInputs inputs = new(targets.Hdr, targets.Depth, motion, output, context.Jitter, context.Mood.Exposure, context.ResetsHistory);
        upscaler.Upscale(commands, inputs);

        commands.Barrier(output, ResourceState.ShaderWrite);
        commands.SetPipeline(_mask);
        Vector2 renderSize = new(targets.Width, targets.Height);
        UpscaleMaskConstants constants = new(targets.Hdr.BindlessIndex, output.BindlessIndex, context.ClampSampler.BindlessIndex, 0, context.Jitter, renderSize);
        commands.PushConstants(constants);
        commands.Dispatch((targets.OutputWidth + GroupSize - 1) / GroupSize, (targets.OutputHeight + GroupSize - 1) / GroupSize);
        commands.Barrier(output, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        _mask.Dispose();
    }
}
