using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.GlobalIllumination;

/// <summary>
/// Finds the probes a frame lights with, and while the frame traces rays traces the scene's first irradiance volume's probes live,
/// as dynamic diffuse global illumination does (Majercik et al. 2019): every frame each probe sends rays over the sphere, finds the
/// light each meets (Shaders/ProbeRays.hlsl), lit by the probes themselves too so light bounces on from frame to frame, and folds
/// them into its light and its map of distances, keeping most of the frames before. It runs after the acceleration structures are
/// built and before the geometry pass. Otherwise a frame lights with another renderer's probes
/// (<see cref="RenderContext.ProbesFrom"/>) or with those baked for the scene's first volume with a file (<see cref="ProbeCache"/>).
/// Its pipelines and buffers exist only while it traces.
/// </summary>
internal sealed class ProbePass : IRenderPass
{
    /// <summary>The rays each probe traces a frame while live.</summary>
    public const int LiveRays = 128;

    /// <summary>The rays each probe traces a bounce while baking.</summary>
    public const int BakeRays = 1024;

    // How much of its light and distances a live probe keeps from the frames before.
    private const float Hysteresis = 0.97f;

    private const int GroupSize = 64;

    // A ray's direction and distance, then the light it met.
    private const int RaySize = 32;

    private readonly IGraphicsDevice _device;
    private readonly ShaderLibrary _shaders;
    private readonly ProbeCache _cache;
    private IPipeline? _trace;
    private IPipeline? _update;
    private IBuffer? _probes;
    private IBuffer? _rays;
    private ProbeSource? _traced;
    private RenderProbeVolume? _bake;
    private bool _history;
    private uint _number;

    public ProbePass(IGraphicsDevice device, ShaderLibrary shaders, ProbeCache cache)
    {
        _device = device;
        _shaders = shaders;
        _cache = cache;
    }

    /// <summary>The probes it traces this frame, which a renderer sharing them lights with too, or null.</summary>
    public ProbeSource? Live { get; private set; }

    /// <summary>
    /// A volume to bake in place of the scene's while frames trace rays: each frame traces it with <see cref="BakeRays"/> rays a probe
    /// and keeps nothing of the frame before but the light the rays meet, so each adds a bounce. Setting it starts from dark probes.
    /// </summary>
    public RenderProbeVolume? Bake
    {
        get => _bake;
        set
        {
            _bake = value;
            Release();
        }
    }

    private int RayCount
    {
        get
        {
            if (_bake is null)
            {
                return LiveRays;
            }

            return BakeRays;
        }
    }

    public void Prepare(RenderContext context)
    {
        Live = null;
        context.ProbeSource = Find(context);
    }

    public void Execute(RenderContext context)
    {
        if (Live is not { } live || !ReferenceEquals(context.ProbeSource, live))
        {
            return;
        }

        int rays = RayCount;
        float hysteresis = Hysteresis;
        if (_bake is not null || !_history)
        {
            hysteresis = 0;
        }

        IrradianceVolume volume = live.Volume;
        int count = volume.ProbesX * volume.ProbesY * volume.ProbesZ;
        IBuffer probes = _probes!;
        IBuffer rayBuffer = _rays!;
        ProbeRayConstants constants = new(context.FrameBuffer, rayBuffer.BindlessIndex, probes.BindlessIndex, (uint)rays, _number, hysteresis);
        ICommandList commands = context.Commands;
        commands.Barrier(probes, ResourceState.ShaderRead);
        commands.Barrier(rayBuffer, ResourceState.ShaderWrite);
        commands.SetPipeline(_trace!);
        commands.PushConstants(constants);
        commands.Dispatch((rays + GroupSize - 1) / GroupSize, count);
        commands.Barrier(rayBuffer, ResourceState.ShaderRead);
        commands.Barrier(probes, ResourceState.ShaderWrite);
        commands.SetPipeline(_update!);
        commands.PushConstants(constants);
        commands.Dispatch(count);
        commands.Barrier(probes, ResourceState.ShaderRead);
        _history = true;
        _number++;
    }

    /// <summary>Records a copy of the traced probes, laid out as <see cref="IrradianceProbes.WriteTo"/> lays them out, into the buffer.</summary>
    public void CopyProbes(ICommandList commands, IBuffer destination)
    {
        IBuffer probes = _probes ?? throw new InvalidOperationException("No probes were traced to copy.");
        commands.Barrier(probes, ResourceState.CopySource);
        commands.CopyBuffer(probes, 0, destination, 0, probes.Description.Size);
    }

    public void Dispose()
    {
        Release();
    }

    private ProbeSource? Find(RenderContext context)
    {
        if (_bake is { } bake && context.RayTracing)
        {
            return Track(bake.Volume, bake.Center);
        }

        if (!context.Probes)
        {
            Release();
            return null;
        }

        if (context.ProbesFrom is { } owner)
        {
            Release();
            return owner.Live ?? _cache.Find(context.Scene);
        }

        List<RenderProbeVolume> volumes = context.Scene.ProbeVolumes;
        if (!context.RayTracing || !context.LiveProbes || volumes.Count == 0)
        {
            Release();
            return _cache.Find(context.Scene);
        }

        RenderProbeVolume first = volumes[0];
        return Track(first.Volume, first.Center);
    }

    /// <summary>The live probes of the volume, made dark and without history the first frame it is traced, or when it moves or changes.</summary>
    private ProbeSource Track(IrradianceVolume volume, Vector3 center)
    {
        if (_traced is null || !Matches(_traced, volume, center))
        {
            ReleaseBuffers();
            IrradianceProbes dark = new(volume.ProbesX, volume.ProbesY, volume.ProbesZ);
            byte[] bytes = new byte[dark.ByteCount];
            dark.WriteTo(bytes);
            BufferUsage usage = BufferUsage.ShaderRead | BufferUsage.ShaderWrite | BufferUsage.CopySource;
            BufferDescription description = new(bytes.Length, usage) { Name = "Live probes" };
            _probes = _device.CreateBuffer(description, bytes);
            _traced = new ProbeSource(volume, center, null, _probes);
            _history = false;
        }

        long raysSize = (long)volume.ProbesX * volume.ProbesY * volume.ProbesZ * RayCount * RaySize;
        EnsureRays(raysSize);
        EnsurePipelines();
        Live = _traced;
        return _traced;
    }

    private static bool Matches(ProbeSource traced, in IrradianceVolume volume, Vector3 center)
    {
        IrradianceVolume tracedVolume = traced.Volume;
        bool counts = tracedVolume.ProbesX == volume.ProbesX && tracedVolume.ProbesY == volume.ProbesY && tracedVolume.ProbesZ == volume.ProbesZ;
        return counts && tracedVolume.Size == volume.Size && traced.Center == center;
    }

    /// <summary>A buffer for the frame's rays at least that big; a bigger one replaces a smaller one, released once the GPU is done with it.</summary>
    private void EnsureRays(long size)
    {
        if (_rays is not null && _rays.Description.Size >= size)
        {
            return;
        }

        _rays?.Dispose();
        BufferDescription description = new(size, BufferUsage.ShaderRead | BufferUsage.ShaderWrite) { Name = "Probe rays" };
        _rays = _device.CreateBuffer(description);
    }

    private void EnsurePipelines()
    {
        if (_trace is not null)
        {
            return;
        }

        ComputePipelineDescription trace = new() { ComputeShader = _shaders.Load("ProbeRays", "TraceMain"), Name = "Probe rays" };
        _trace = _device.CreateComputePipeline(trace);
        ComputePipelineDescription update = new() { ComputeShader = _shaders.Load("ProbeRays", "UpdateMain"), Name = "Probe update" };
        _update = _device.CreateComputePipeline(update);
    }

    private void ReleaseBuffers()
    {
        _probes?.Dispose();
        _probes = null;
        _rays?.Dispose();
        _rays = null;
        _traced = null;
        Live = null;
    }

    private void Release()
    {
        ReleaseBuffers();
        _trace?.Dispose();
        _trace = null;
        _update?.Dispose();
        _update = null;
    }
}
