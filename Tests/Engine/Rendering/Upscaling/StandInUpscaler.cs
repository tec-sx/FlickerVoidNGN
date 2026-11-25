using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Upscaling;

namespace FlickerVoid.Engine.Rendering.Tests.Upscaling;

/// <summary>
/// Stands in for DLSS: frames are drawn at two thirds of the output's size for Quality and at its size for DLAA, and it fills the
/// output with the nearest pixel of each where its jitter moved the surface (Shaders/StandInUpscale.hlsl), noting every frame.
/// </summary>
internal sealed class StandInUpscaler : IUpscaler
{
    private const int GroupSize = 8;

    private readonly IPipeline _pipeline;

    public StandInUpscaler(IGraphicsDevice device, UpscalerDescription description)
    {
        Description = description;
        RenderWidth = description.OutputWidth;
        RenderHeight = description.OutputHeight;
        if (description.Mode == UpscaleMode.Quality)
        {
            RenderWidth = (int)MathF.Round(description.OutputWidth * 2f / 3);
            RenderHeight = (int)MathF.Round(description.OutputHeight * 2f / 3);
        }

        string folder = Path.Combine(AppContext.BaseDirectory, "Content", "Tests");
        ShaderLibrary shaders = new(folder);
        ComputePipelineDescription pipeline = new() { ComputeShader = shaders.Load("StandInUpscale", "ComputeMain"), Name = "Stand-in upscale" };
        _pipeline = device.CreateComputePipeline(pipeline);
    }

    public UpscalerDescription Description { get; }

    public int RenderWidth { get; }

    public int RenderHeight { get; }

    /// <summary>What each frame gave it, in order.</summary>
    public List<UpscaleInputs> Frames { get; } = [];

    public bool Disposed { get; private set; }

    public void Upscale(ICommandList commands, in UpscaleInputs inputs)
    {
        Frames.Add(inputs);
        int outputWidth = inputs.Output.Description.Width;
        int outputHeight = inputs.Output.Description.Height;
        Vector2 renderSize = new(inputs.Color.Description.Width, inputs.Color.Description.Height);
        Vector2 scale = renderSize / new Vector2(outputWidth, outputHeight);
        commands.SetPipeline(_pipeline);
        StandInUpscaleConstants constants = new(inputs.Color.BindlessIndex, inputs.Output.BindlessIndex, scale, inputs.Jitter, renderSize);
        commands.PushConstants(constants);
        commands.Dispatch((outputWidth + GroupSize - 1) / GroupSize, (outputHeight + GroupSize - 1) / GroupSize);
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        Disposed = true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct StandInUpscaleConstants(uint Color, uint Output, Vector2 Scale, Vector2 Jitter, Vector2 RenderSize);
}
