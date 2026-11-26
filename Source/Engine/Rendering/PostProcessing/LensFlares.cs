using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>Draws the lens flare of the lens's flare file from the half-size scene into <see cref="ScreenTargets.Flare"/>, after Froyok's.</summary>
internal sealed class LensFlares : IDisposable
{
    private const string Shader = "Flare";
    private const int GroupSize = 8;

    private readonly IPipeline _threshold;
    private readonly IPipeline _blur;
    private readonly IPipeline _ghosts;
    private readonly IPipeline _finish;
    private readonly FrameBufferRing _ghostData;

    public LensFlares(IGraphicsDevice device, ShaderLibrary shaders, int framesInFlight)
    {
        _threshold = Create(device, shaders, "ThresholdMain", "Flare threshold");
        _blur = Create(device, shaders, "BlurMain", "Flare blur");
        _ghosts = Create(device, shaders, "GhostsMain", "Flare ghosts");
        _finish = Create(device, shaders, "FinishMain", "Flare finish");
        _ghostData = new FrameBufferRing(device, framesInFlight);
    }

    /// <summary>Draws the flare from <paramref name="halfScene"/>, the scene at half the output's size; leaves the flare readable.</summary>
    public void Run(ICommandList commands, ScreenTargets targets, ITexture halfScene, FlareFile file, LightingMood mood, ISampler clamp)
    {
        _ghostData.Advance();
        int count = Math.Min(file.Ghosts.Count, FlareFile.MaxGhosts);
        GpuFlareGhost[] ghosts = new GpuFlareGhost[Math.Max(count, 1)];
        for (int index = 0; index < count; index++)
        {
            FlareGhost ghost = file.Ghosts[index];
            ghosts[index] = new GpuFlareGhost(ghost.Color, ghost.Scale);
        }

        IBuffer data = _ghostData.Get(ghosts.Length * Marshal.SizeOf<GpuFlareGhost>());
        data.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<GpuFlareGhost>(ghosts)));

        FlareConstants settings = new(
            Vector2.Zero,
            Vector2.Zero,
            0,
            0,
            clamp.BindlessIndex,
            data.BindlessIndex,
            (uint)count,
            mood.FlareThreshold,
            mood.FlareThresholdRange,
            mood.FlareIntensity,
            file.ChromaShift,
            file.EdgeFade,
            file.HaloRadius,
            file.HaloWidth,
            file.CenterColor,
            file.HaloIntensity,
            file.EdgeColor,
            file.HaloChroma,
            (float)targets.Flare.Description.Width / targets.Flare.Description.Height);
        Dispatch(commands, _threshold, halfScene, targets.FlarePing, settings);
        Dispatch(commands, _blur, targets.FlarePing, targets.FlarePong, settings);
        Dispatch(commands, _ghosts, targets.FlarePong, targets.FlarePing, settings);
        Dispatch(commands, _finish, targets.FlarePing, targets.Flare, settings);
    }

    public void Dispose()
    {
        _ghostData.Dispose();
        _finish.Dispose();
        _ghosts.Dispose();
        _blur.Dispose();
        _threshold.Dispose();
    }

    private static IPipeline Create(IGraphicsDevice device, ShaderLibrary shaders, string entryPoint, string name)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load(Shader, entryPoint), Name = name };
        return device.CreateComputePipeline(description);
    }

    private static void Dispatch(ICommandList commands, IPipeline pipeline, ITexture source, ITexture destination, FlareConstants settings)
    {
        commands.SetPipeline(pipeline);
        commands.Barrier(destination, ResourceState.ShaderWrite);
        Vector2 sourceTexel = new(1f / source.Description.Width, 1f / source.Description.Height);
        Vector2 destinationSize = new(destination.Description.Width, destination.Description.Height);
        FlareConstants constants = settings with
        {
            SourceTexel = sourceTexel,
            DestinationSize = destinationSize,
            Source = source.BindlessIndex,
            Destination = destination.BindlessIndex,
        };
        commands.PushConstants(constants);
        int groupsX = (destination.Description.Width + GroupSize - 1) / GroupSize;
        int groupsY = (destination.Description.Height + GroupSize - 1) / GroupSize;
        commands.Dispatch(groupsX, groupsY);
        commands.Barrier(destination, ResourceState.ShaderRead);
    }
}
