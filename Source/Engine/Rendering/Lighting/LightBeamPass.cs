using System.Diagnostics;
using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Places beams of sunlight by itself where light breaks through a gap, and adds them to the lit scene before the depth fade and
/// the haze, while the frame traces rays and the mood's <see cref="LightingMood.BeamIntensity"/> is above 0 (LightBeams.hlsl): a
/// grid of seeds anchored to the world round the focus (else the camera) finds the ground under it and the share of the sun its
/// rays reach, and each beam eases toward its seed's strength and height over about half a second, so moving leaves make beams
/// breathe rather than flicker. Its pipelines and beams exist only while it draws.
/// </summary>
internal sealed class LightBeamPass : IRenderPass
{
    /// <summary>Seeds along each side of the grid.</summary>
    public const int GridSize = 16;

    public const int BeamCount = GridSize * GridSize;

    private const int BeamSize = 32;

    private const int UpdateGroup = 64;

    private const int GroupSize = 8;

    /// <summary>How long a beam takes to come most of the way (1 - 1/e) to its seed's strength.</summary>
    public const double EaseSeconds = 0.5;

    /// <summary>The longest step the easing takes between two frames, so a stall doesn't snap the beams.</summary>
    private const double MaxEaseStep = 0.1;

    /// <summary>How far above the focus seeds look down for the ground, so they land on floors under roofs and ceilings.</summary>
    private const float Lift = 1.5f;

    private readonly IGraphicsDevice _device;
    private readonly ShaderLibrary _shaders;
    private IPipeline? _update;
    private IPipeline? _draw;
    private IBuffer? _beams;
    private bool _hasHistory;
    private long _lastTimestamp;

    public LightBeamPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _device = device;
        _shaders = shaders;
    }

    /// <summary>Whether the frame draws beams: it traces rays and the mood asks for them.</summary>
    public static bool HasBeams(RenderContext context)
    {
        return context.RayTracing && context.Mood.BeamIntensity > 0;
    }

    public void Prepare(RenderContext context)
    {
        if (!HasBeams(context))
        {
            Release();
            return;
        }

        if (_update is null)
        {
            ComputePipelineDescription update = new() { ComputeShader = _shaders.Load("LightBeams", "UpdateMain"), Name = "Light beam update" };
            _update = _device.CreateComputePipeline(update);
            ComputePipelineDescription draw = new() { ComputeShader = _shaders.Load("LightBeams", "DrawMain"), Name = "Light beam draw" };
            _draw = _device.CreateComputePipeline(draw);
            BufferDescription beams = new(BeamCount * BeamSize, BufferUsage.ShaderRead | BufferUsage.ShaderWrite) { Name = "Light beams" };
            _beams = _device.CreateBuffer(beams);
            _hasHistory = false;
        }
    }

    public void Execute(RenderContext context)
    {
        if (!HasBeams(context) || _update is null || _beams is null)
        {
            return;
        }

        LightBeamConstants constants = CreateConstants(context);
        ICommandList commands = context.Commands;
        commands.Barrier(_beams, ResourceState.ShaderWrite);
        commands.SetPipeline(_update);
        commands.PushConstants(constants);
        commands.Dispatch((BeamCount + UpdateGroup - 1) / UpdateGroup);
        commands.Barrier(_beams, ResourceState.ShaderRead);
        commands.Barrier(context.Targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(context.Targets.Hdr, ResourceState.ShaderWrite);
        commands.SetPipeline(_draw!);
        commands.PushConstants(constants);
        commands.Dispatch((context.Targets.Width + GroupSize - 1) / GroupSize, (context.Targets.Height + GroupSize - 1) / GroupSize);
        commands.Barrier(context.Targets.Hdr, ResourceState.ShaderRead);
        _hasHistory = true;
    }

    public void Dispose()
    {
        Release();
    }

    private LightBeamConstants CreateConstants(RenderContext context)
    {
        LightingMood mood = context.Mood;
        float blend = Ease(context);
        Matrix4x4.Invert(context.View.View, out Matrix4x4 cameraWorld);
        Vector3 center = context.Focus ?? cameraWorld.Translation;
        float spacing = MathF.Max(mood.BeamReach, 0.5f) * 2 / GridSize;
        int cellX = (int)MathF.Floor(center.X / spacing);
        int cellZ = (int)MathF.Floor(center.Z / spacing);
        Vector3 color = mood.BeamColor * mood.BeamIntensity;
        float halfWidth = MathF.Max(mood.BeamWidth, 0.01f) / 2;
        return new LightBeamConstants(center, spacing, color, halfWidth, cellX, cellZ, blend, Lift, context.FrameBuffer, _beams!.BindlessIndex, context.Targets.Hdr.BindlessIndex, 0);
    }

    /// <summary>How far the beams move toward their seeds this frame: all the way without history, else by the time since the last.</summary>
    private float Ease(RenderContext context)
    {
        long now = Stopwatch.GetTimestamp();
        double seconds = MaxEaseStep;
        if (_lastTimestamp != 0)
        {
            seconds = Math.Min(Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds, MaxEaseStep);
        }

        _lastTimestamp = now;
        if (!_hasHistory || context.ResetsHistory)
        {
            return 1;
        }

        return (float)(1 - Math.Exp(-seconds / EaseSeconds));
    }

    private void Release()
    {
        _update?.Dispose();
        _update = null;
        _draw?.Dispose();
        _draw = null;
        _beams?.Dispose();
        _beams = null;
        _hasHistory = false;
    }
}
