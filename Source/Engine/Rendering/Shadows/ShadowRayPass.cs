using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// Traces the shadows of the sun and of the lamps given a channel (<see cref="RenderContext.RayShadowLights"/>), and the ambient
/// light's occlusion, while the frame traces rays, a ray a pixel each frame for each, and filters them with the frames before
/// into the traced shadows and occlusion the lighting pass reads (<see cref="RenderContext.RayShadows"/> and
/// <see cref="RenderContext.RayOcclusion"/>), after the geometry pass, from whose surfaces the rays start; while the upscaler
/// denoises the frame (<see cref="RenderContext.Denoises"/>), the lighting pass reads the frame's own rays instead, unfiltered.
/// Its pipelines and textures exist only while frames trace rays, and those of the filter only while it filters.
/// </summary>
internal sealed class ShadowRayPass : IRenderPass
{
    /// <summary>How many lamps' shadows a frame traces; the sun's takes the first of the four channels.</summary>
    public const int LampSlots = 3;

    private const int GroupSize = 8;

    private readonly IGraphicsDevice _device;
    private readonly ShaderLibrary _shaders;

    // The filtered shadows and each pixel's history (the filtered occlusion, how many frames it averages and its surface's view
    // depth), this frame's and the frame before's in turn.
    private readonly ITexture?[] _visibility = new ITexture?[2];
    private readonly ITexture?[] _history = new ITexture?[2];
    private IPipeline? _trace;
    private IPipeline? _filter;
    private ITexture? _raw;
    private ITexture? _rawOcclusion;
    private int _width;
    private int _height;
    private int _current;
    private bool _historyKept;
    private uint _number;
    private Vector4 _previousDepthAxis;

    public ShadowRayPass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _device = device;
        _shaders = shaders;
    }

    public void Prepare(RenderContext context)
    {
        if (!context.RayTracing)
        {
            Release();
            return;
        }

        bool filters = !context.Denoises;
        Ensure(context.RenderWidth, context.RenderHeight, filters);
        if (!filters)
        {
            context.RayShadows = _raw;
            context.RayOcclusion = _rawOcclusion;
            return;
        }

        if (context.ResetsHistory)
        {
            _historyKept = false;
        }

        _current = 1 - _current;
        context.RayShadows = _visibility[_current];
        context.RayOcclusion = _history[_current];
    }

    public void Execute(RenderContext context)
    {
        if (context.RayShadows is null)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ITexture raw = _raw!;
        ITexture rawOcclusion = _rawOcclusion!;
        bool filters = !context.Denoises;
        ShadowRayConstants constants = CreateConstants(context, filters);
        commands.Barrier(context.Targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(context.GBuffer.Normal, ResourceState.ShaderRead);
        commands.Barrier(context.GBuffer.Motion, ResourceState.ShaderRead);
        commands.Barrier(raw, ResourceState.ShaderWrite);
        commands.Barrier(rawOcclusion, ResourceState.ShaderWrite);
        commands.SetPipeline(_trace!);
        commands.PushConstants(constants);
        int groupsX = (context.RenderWidth + GroupSize - 1) / GroupSize;
        int groupsY = (context.RenderHeight + GroupSize - 1) / GroupSize;
        commands.Dispatch(groupsX, groupsY);
        commands.Barrier(raw, ResourceState.ShaderRead);
        commands.Barrier(rawOcclusion, ResourceState.ShaderRead);
        _number++;
        if (!filters)
        {
            return;
        }

        ITexture visibility = _visibility[_current]!;
        ITexture history = _history[_current]!;
        commands.Barrier(visibility, ResourceState.ShaderWrite);
        commands.Barrier(history, ResourceState.ShaderWrite);
        commands.SetPipeline(_filter!);
        commands.PushConstants(constants);
        commands.Dispatch(groupsX, groupsY);
        commands.Barrier(visibility, ResourceState.ShaderRead);
        commands.Barrier(history, ResourceState.ShaderRead);
        _previousDepthAxis = context.View.DepthAxis;
        _historyKept = true;
    }

    public void Dispose()
    {
        Release();
    }

    /// <summary>The trace's constants, and the filter's when it filters; without the filter, its textures are none.</summary>
    private ShadowRayConstants CreateConstants(RenderContext context, bool filters)
    {
        uint visibility = GpuLight.None;
        uint history = GpuLight.None;
        uint previousVisibility = GpuLight.None;
        uint previousHistory = GpuLight.None;
        if (filters)
        {
            visibility = _visibility[_current]!.BindlessIndex;
            history = _history[_current]!.BindlessIndex;
        }

        if (filters && _historyKept)
        {
            int previous = 1 - _current;
            previousVisibility = _visibility[previous]!.BindlessIndex;
            previousHistory = _history[previous]!.BindlessIndex;
        }

        float sunRadius = MathF.Tan(float.DegreesToRadians(context.Mood.SunAngle) / 2);
        List<uint> lamps = context.RayShadowLights;
        uint lamp0 = GetLamp(lamps, 0);
        uint lamp1 = GetLamp(lamps, 1);
        uint lamp2 = GetLamp(lamps, 2);
        return new ShadowRayConstants(
            _previousDepthAxis,
            context.FrameBuffer,
            _raw!.BindlessIndex,
            _rawOcclusion!.BindlessIndex,
            visibility,
            history,
            previousVisibility,
            previousHistory,
            _number,
            sunRadius,
            lamp0,
            lamp1,
            lamp2);
    }

    /// <summary>The place among the frame's lights of the lamp traced in the slot, or none.</summary>
    private static uint GetLamp(List<uint> lamps, int slot)
    {
        if (slot < lamps.Count)
        {
            return lamps[slot];
        }

        return GpuLight.None;
    }

    /// <summary>
    /// Creates the pipelines the first time frames trace rays, and the textures at the frame's size, the filter's only while it
    /// filters; new textures start without history.
    /// </summary>
    private void Ensure(int width, int height, bool filters)
    {
        if (_trace is null)
        {
            ComputePipelineDescription trace = new() { ComputeShader = _shaders.Load("ShadowRays", "TraceMain"), Name = "Shadow rays" };
            _trace = _device.CreateComputePipeline(trace);
            ComputePipelineDescription filter = new() { ComputeShader = _shaders.Load("ShadowRays", "FilterMain"), Name = "Shadow ray filter" };
            _filter = _device.CreateComputePipeline(filter);
        }

        if (_raw is null || width != _width || height != _height)
        {
            ReleaseTextures();
            _width = width;
            _height = height;
            _raw = CreateTexture(PixelFormat.Rgba8UNorm, "Shadow rays");
            _rawOcclusion = CreateTexture(PixelFormat.Rgba8UNorm, "Occlusion rays");
        }

        bool filtered = _visibility[0] is not null;
        if (filters == filtered)
        {
            return;
        }

        ReleaseFiltered();
        if (!filters)
        {
            return;
        }

        for (int index = 0; index < 2; index++)
        {
            _visibility[index] = CreateTexture(PixelFormat.Rgba16Float, $"Traced shadows {index}");
            _history[index] = CreateTexture(PixelFormat.Rgba16Float, $"Traced shadow history {index}");
        }
    }

    private ITexture CreateTexture(PixelFormat format, string name)
    {
        TextureDescription description = new(_width, _height, format, TextureUsage.ShaderRead | TextureUsage.ShaderWrite) { Name = name };
        return _device.CreateTexture(description);
    }

    private void ReleaseTextures()
    {
        _raw?.Dispose();
        _raw = null;
        _rawOcclusion?.Dispose();
        _rawOcclusion = null;
        ReleaseFiltered();
    }

    private void ReleaseFiltered()
    {
        for (int index = 0; index < 2; index++)
        {
            _visibility[index]?.Dispose();
            _visibility[index] = null;
            _history[index]?.Dispose();
            _history[index] = null;
        }

        _historyKept = false;
    }

    private void Release()
    {
        ReleaseTextures();
        _trace?.Dispose();
        _trace = null;
        _filter?.Dispose();
        _filter = null;
    }
}
