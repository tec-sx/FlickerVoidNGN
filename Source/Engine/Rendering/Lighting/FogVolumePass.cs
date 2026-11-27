using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Lights the fog once a frame in a small grid along the view (160 by 90 froxels, 64 slices out to the mood's fog range:
/// logarithmic in a perspective view, even in an orthographic one), with the sun through its shadow map, the cluster lights and
/// the probes (Lighting.hlsl), keeps most of the frame before's lighting by reprojecting it, and integrates every column of the
/// grid front to back, so the lighting pass reads the fog in front of any surface with one lookup. Fog volumes add their density
/// and colour to the mood's, nearest the focus first, and a drifting noise breaks both up. It exists only while the mood has fog
/// or the scene has a fog volume.
/// </summary>
internal sealed class FogVolumePass : IRenderPass
{
    public const int Width = 160;

    public const int Height = 90;

    public const int Slices = 64;

    // A froxel holds a float4: the light scattered, and the density or the light that gets through.
    private const int CellSize = 16;

    private const int GroupSize = 8;

    private const int SliceGroup = 4;

    /// <summary>The fog volumes a frame takes at most, nearest the focus first.</summary>
    public const int MaxVolumes = 32;

    /// <summary>The longest step the noise's drift takes between two frames, so a stall doesn't jump the fog.</summary>
    private const double MaxDriftStep = 0.1;

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _light;
    private readonly IPipeline _integrate;
    private IBuffer? _lit;
    private IBuffer? _history;
    private IBuffer? _integrated;
    private readonly FrameBufferRing _fog;
    private RenderFogVolume[] _order = [];
    private float[] _distances = [];
    private readonly GpuFogVolume[] _gpuVolumes = new GpuFogVolume[MaxVolumes];
    private bool _hasHistory;
    private uint _number;
    private long _lastTimestamp;
    private Vector3 _drift;

    public FogVolumePass(IGraphicsDevice device, ShaderLibrary shaders, int framesInFlight)
    {
        _device = device;
        _fog = new FrameBufferRing(device, framesInFlight);
        ComputePipelineDescription lightDescription = new() { ComputeShader = shaders.Load("Lighting", "FogLightMain"), Name = "Fog light" };
        _light = device.CreateComputePipeline(lightDescription);
        ComputePipelineDescription integrateDescription = new() { ComputeShader = shaders.Load("Lighting", "FogIntegrateMain"), Name = "Fog integrate" };
        _integrate = device.CreateComputePipeline(integrateDescription);
    }

    /// <summary>Whether the frame has fog to light: the mood's, or a fog volume's.</summary>
    public static bool HasFog(RenderContext context)
    {
        return context.Mood.FogDensity > 0 || context.Scene.FogVolumes.Count > 0;
    }

    public void Prepare(RenderContext context)
    {
        if (!HasFog(context))
        {
            Release();
            return;
        }

        if (_integrated is null)
        {
            int size = Width * Height * Slices * CellSize;
            BufferUsage usage = BufferUsage.ShaderRead | BufferUsage.ShaderWrite;
            _lit = _device.CreateBuffer(new BufferDescription(size, usage) { Name = "Fog lit" });
            _history = _device.CreateBuffer(new BufferDescription(size, usage) { Name = "Fog history" });
            _integrated = _device.CreateBuffer(new BufferDescription(size, usage) { Name = "Fog integrated" });
            _hasHistory = false;
        }

        context.FogVolume = _integrated;
    }

    public void Execute(RenderContext context)
    {
        if (context.FogVolume is null || _lit is null || _history is null || _integrated is null)
        {
            return;
        }

        bool keepsHistory = _hasHistory && !context.ResetsHistory;
        uint keep = 0;
        if (keepsHistory)
        {
            keep = 1;
        }

        IBuffer fog = WriteFog(context);
        LightingConstants constants = new(context.FrameBuffer, 0, _lit.BindlessIndex, _history.BindlessIndex, _integrated.BindlessIndex, keep, _number, fog.BindlessIndex);
        ICommandList commands = context.Commands;
        commands.Barrier(_history, ResourceState.ShaderRead);
        commands.Barrier(_lit, ResourceState.ShaderWrite);
        commands.SetPipeline(_light);
        commands.PushConstants(constants);
        commands.Dispatch((Width + GroupSize - 1) / GroupSize, (Height + GroupSize - 1) / GroupSize, Slices / SliceGroup);
        commands.Barrier(_lit, ResourceState.ShaderRead);
        commands.Barrier(_integrated, ResourceState.ShaderWrite);
        commands.SetPipeline(_integrate);
        commands.PushConstants(constants);
        commands.Dispatch((Width + GroupSize - 1) / GroupSize, (Height + GroupSize - 1) / GroupSize);
        commands.Barrier(_integrated, ResourceState.ShaderRead);
        (_lit, _history) = (_history, _lit);
        _hasHistory = true;
        _number++;
    }

    public void Dispose()
    {
        _light.Dispose();
        _integrate.Dispose();
        _fog.Dispose();
        Release();
    }

    /// <summary>Writes the noise's drift and the fog volumes nearest the focus (or the camera) into this frame's fog buffer.</summary>
    private IBuffer WriteFog(RenderContext context)
    {
        LightingMood mood = context.Mood;
        long now = Stopwatch.GetTimestamp();
        if (_lastTimestamp != 0)
        {
            double seconds = Math.Min(Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds, MaxDriftStep);
            Vector3 wind = mood.FogWind;
            if (wind.LengthSquared() > 0)
            {
                wind = Vector3.Normalize(wind);
            }

            _drift += wind * (float)(mood.FogWindSpeed * seconds);
        }

        _lastTimestamp = now;
        Matrix4x4.Invert(context.View.View, out Matrix4x4 cameraWorld);
        Vector3 origin = context.Focus ?? cameraWorld.Translation;
        int count = SelectVolumes(context.Scene.FogVolumes, origin);
        for (int index = 0; index < count; index++)
        {
            _gpuVolumes[index] = GpuFogVolume.From(_order[index]);
        }

        GpuFogHeader header = new((uint)count, mood.FogNoise, mood.FogNoiseScale, 0, _drift);
        int headerSize = Marshal.SizeOf<GpuFogHeader>();
        ReadOnlySpan<byte> volumeBytes = MemoryMarshal.AsBytes(_gpuVolumes.AsSpan(0, count));
        _fog.Advance();
        IBuffer buffer = _fog.Get(headerSize + Marshal.SizeOf<GpuFogVolume>() * MaxVolumes);
        buffer.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<GpuFogHeader>(in header)), 0);
        buffer.Write(volumeBytes, headerSize);
        return buffer;
    }

    /// <summary>Orders the volumes nearest the origin first in <see cref="_order"/>; returns how many of them the frame takes.</summary>
    private int SelectVolumes(List<RenderFogVolume> volumes, Vector3 origin)
    {
        int total = volumes.Count;
        if (_order.Length < total)
        {
            _order = new RenderFogVolume[total];
            _distances = new float[total];
        }

        for (int index = 0; index < total; index++)
        {
            RenderFogVolume volume = volumes[index];
            Vector3 offset = volume.Position - origin;
            _order[index] = volume;
            _distances[index] = offset.LengthSquared();
        }

        Array.Sort(_distances, _order, 0, total);
        return Math.Min(total, MaxVolumes);
    }

    private void Release()
    {
        _lit?.Dispose();
        _history?.Dispose();
        _integrated?.Dispose();
        _lit = null;
        _history = null;
        _integrated = null;
        _hasHistory = false;
    }
}
