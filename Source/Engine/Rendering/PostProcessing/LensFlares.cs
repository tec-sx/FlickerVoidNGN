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
    private const string GlareShader = "Glare";
    private const int GroupSize = 8;

    /// <summary>The most bright points that shine a star; the rest are left out.</summary>
    private const int MaxGlarePoints = 1024;

    private const int GlareStreaks = 3;
    private const int GlarePointsSize = 16 + (MaxGlarePoints * 32);

    private readonly IPipeline _threshold;
    private readonly IPipeline _blur;
    private readonly IPipeline _ghosts;
    private readonly IPipeline _finish;
    private readonly IPipeline _findGlare;
    private readonly IPipeline _drawGlare;
    private readonly IBuffer _glarePoints;
    private readonly IBuffer _zero;
    private readonly FrameBufferRing _ghostData;
    private GlareConstants _glare;

    public LensFlares(IGraphicsDevice device, ShaderLibrary shaders, int framesInFlight)
    {
        _threshold = Create(device, shaders, Shader, "ThresholdMain", "Flare threshold");
        _blur = Create(device, shaders, Shader, "BlurMain", "Flare blur");
        _ghosts = Create(device, shaders, Shader, "GhostsMain", "Flare ghosts");
        _finish = Create(device, shaders, Shader, "FinishMain", "Flare finish");
        _findGlare = Create(device, shaders, GlareShader, "FindMain", "Glare find");
        GraphicsPipelineDescription draw = new()
        {
            VertexShader = shaders.Load(GlareShader, "VertexMain"),
            PixelShader = shaders.Load(GlareShader, "PixelMain"),
            ColorFormats = [ScreenTargets.HdrFormat],
            CullMode = CullMode.None,
            Blend = BlendMode.Additive,
            Name = "Glare",
        };
        _drawGlare = device.CreateGraphicsPipeline(draw);
        BufferDescription points = new(GlarePointsSize, BufferUsage.ShaderRead | BufferUsage.ShaderWrite | BufferUsage.CopyDestination, MemoryLocation.Gpu) { Name = "Glare points" };
        _glarePoints = device.CreateBuffer(points);
        BufferDescription zero = new(sizeof(uint), BufferUsage.CopySource, MemoryLocation.Upload) { Name = "Glare zero" };
        _zero = device.CreateBuffer(zero);
        _zero.Write(new byte[sizeof(uint)]);
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
        if (file.GlareIntensity > 0)
        {
            FindGlare(commands, targets, file, mood);
        }

        Dispatch(commands, _blur, targets.FlarePing, targets.FlarePong, settings);
        Dispatch(commands, _ghosts, targets.FlarePong, targets.FlarePing, settings);
        Dispatch(commands, _finish, targets.FlarePing, targets.Flare, settings);
        if (file.GlareIntensity > 0)
        {
            DrawGlare(commands, targets);
        }
    }

    public void Dispose()
    {
        _ghostData.Dispose();
        _zero.Dispose();
        _glarePoints.Dispose();
        _drawGlare.Dispose();
        _findGlare.Dispose();
        _finish.Dispose();
        _ghosts.Dispose();
        _blur.Dispose();
        _threshold.Dispose();
    }

    private void FindGlare(ICommandList commands, ScreenTargets targets, FlareFile file, LightingMood mood)
    {
        commands.Barrier(_glarePoints, ResourceState.CopyDestination);
        commands.CopyBuffer(_zero, 0, _glarePoints, 0, sizeof(uint));
        commands.Barrier(_glarePoints, ResourceState.ShaderWrite);
        commands.SetPipeline(_findGlare);
        GlareConstants constants = Glare(targets, file, mood);
        commands.PushConstants(constants);
        int blocksX = (targets.FlarePing.Description.Width + 1) / 2;
        int blocksY = (targets.FlarePing.Description.Height + 1) / 2;
        commands.Dispatch((blocksX + GroupSize - 1) / GroupSize, (blocksY + GroupSize - 1) / GroupSize);
        commands.Barrier(_glarePoints, ResourceState.ShaderRead);
        _glare = constants;
    }

    private void DrawGlare(ICommandList commands, ScreenTargets targets)
    {
        ITexture flare = targets.Flare;
        commands.Barrier(flare, ResourceState.RenderTarget);
        ColorTarget color = new(flare, LoadAction.Load);
        commands.BeginRendering([color]);
        int width = flare.Description.Width;
        int height = flare.Description.Height;
        commands.SetViewport(new Viewport(0, 0, width, height));
        commands.SetScissor(0, 0, width, height);
        commands.SetPipeline(_drawGlare);
        commands.PushConstants(_glare);
        commands.Draw(6, MaxGlarePoints * GlareStreaks);
        commands.EndRendering();
        commands.Barrier(flare, ResourceState.ShaderRead);
    }

    private GlareConstants Glare(ScreenTargets targets, FlareFile file, LightingMood mood)
    {
        ITexture source = targets.FlarePing;
        float aspect = (float)source.Description.Width / source.Description.Height;
        return new GlareConstants(
            source.BindlessIndex,
            _glarePoints.BindlessIndex,
            (uint)source.Description.Width,
            (uint)source.Description.Height,
            file.GlareThreshold,
            file.GlareIntensity * mood.FlareIntensity,
            file.GlareLength,
            file.GlareWidth,
            file.GlareAngle,
            aspect);
    }

    private static IPipeline Create(IGraphicsDevice device, ShaderLibrary shaders, string shader, string entryPoint, string name)
    {
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load(shader, entryPoint), Name = name };
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
