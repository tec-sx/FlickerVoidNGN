using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Writes the frame's view, sun, lights, shadows, light clusters, probes, indoor volumes and cutaway into the frame buffer ring, after every pass
/// has prepared and before any executes.
/// </summary>
internal sealed class FrameDataWriter : IDisposable
{
    private readonly LightClusters _clusters = new();
    private readonly FrameBufferRing _frames;
    private readonly ProbeCache _probes;

    public FrameDataWriter(IGraphicsDevice device, ProbeCache probes, int framesInFlight)
    {
        _frames = new FrameBufferRing(device, framesInFlight);
        _probes = probes;
    }

    /// <summary>Moves to the next frame in flight and returns its slot.</summary>
    public int Advance()
    {
        return _frames.Advance();
    }

    public IBuffer Write(RenderContext context)
    {
        Camera camera = context.Camera;
        CameraView view = context.View;
        ReadOnlySpan<LightBounds> bounds = CollectionsMarshal.AsSpan(context.LightBounds);
        _clusters.Build(camera, view, bounds);

        ReadOnlySpan<byte> lightBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(context.Lights));
        ReadOnlySpan<byte> shadowBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(context.Shadows));
        ReadOnlySpan<byte> rangeBytes = MemoryMarshal.AsBytes(_clusters.Ranges);
        ReadOnlySpan<byte> indexBytes = MemoryMarshal.AsBytes(_clusters.Indices);
        int lightsOffset = Marshal.SizeOf<FrameData>();
        int shadowsOffset = lightsOffset + lightBytes.Length;
        int clustersOffset = shadowsOffset + shadowBytes.Length;
        int indicesOffset = clustersOffset + rangeBytes.Length;
        GpuIndoor[] indoors = CollectIndoors(context.Scene);
        ReadOnlySpan<byte> indoorBytes = MemoryMarshal.AsBytes(indoors.AsSpan());
        int indoorsOffset = indicesOffset + indexBytes.Length;
        int size = indoorsOffset + indoorBytes.Length;

        // A pixel's view depth is the dot product of its world position and the view matrix's negated third column.
        Matrix4x4 viewMatrix = view.View;
        Vector4 depthAxis = -new Vector4(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33, viewMatrix.M43);
        uint logarithmic = 0;
        if (_clusters.LogarithmicSlices)
        {
            logarithmic = 1;
        }

        uint probeBuffer = GpuLight.None;
        Vector3 probeMin = Vector3.Zero;
        Vector3 probeSize = Vector3.Zero;
        IrradianceVolume volume = new();
        ProbeSource? probes = context.ProbeSource;
        if (probes?.Probes is not null)
        {
            probeBuffer = _probes.Upload(probes.Probes);
            volume = probes.Volume;
            probeSize = volume.Size;
            probeMin = probes.Center - (volume.Size / 2);
        }

        RenderIndoor cutaway = default;
        uint cut = 0;
        if (context.Scene.Cutaway is { } looked)
        {
            cutaway = looked;
            cut = 1;
        }

        Matrix4x4.Invert(viewMatrix, out Matrix4x4 cameraWorld);
        Matrix4x4.Invert(view.ViewProjection, out Matrix4x4 inverseViewProjection);
        LightingMood mood = context.Mood;
        Vector3 sunDirection = Vector3.Normalize(mood.SunDirection);
        FrameData data = new(
            view.ViewProjection,
            depthAxis,
            sunDirection,
            mood.Ambient,
            new Vector2(context.Width, context.Height),
            _clusters.SliceScale,
            _clusters.SliceBias,
            logarithmic,
            (uint)context.Lights.Count,
            (uint)clustersOffset,
            (uint)indicesOffset,
            (uint)shadowsOffset,
            context.SunShadow,
            context.ShadowAtlas,
            context.ClampSampler.BindlessIndex,
            probeMin,
            probeBuffer,
            probeSize,
            (uint)volume.ProbesX,
            (uint)volume.ProbesY,
            (uint)volume.ProbesZ,
            0,
            0,
            cameraWorld.Translation,
            camera.NearPlane,
            mood.FogColor,
            mood.FogDensity,
            mood.FogAnisotropy,
            mood.FogHeight,
            mood.FogHeightFalloff,
            0,
            inverseViewProjection,
            context.Targets.ScreenShadows.BindlessIndex,
            mood.AmbientOcclusion,
            mood.AmbientOcclusionRadius,
            mood.ContactShadowLength,
            cutaway.Min,
            cut,
            cutaway.Max,
            cutaway.OutsideMuted,
            (uint)indoors.Length,
            (uint)indoorsOffset,
            0,
            0);

        IBuffer frame = _frames.Get(size);
        ReadOnlySpan<FrameData> header = new(in data);
        frame.Write(MemoryMarshal.AsBytes(header));
        frame.Write(lightBytes, lightsOffset);
        frame.Write(shadowBytes, shadowsOffset);
        frame.Write(rangeBytes, clustersOffset);
        frame.Write(indexBytes, indicesOffset);
        frame.Write(indoorBytes, indoorsOffset);
        return frame;
    }

    private static GpuIndoor[] CollectIndoors(RenderScene scene)
    {
        GpuIndoor[] indoors = new GpuIndoor[scene.Indoors.Count];
        for (int index = 0; index < indoors.Length; index++)
        {
            RenderIndoor indoor = scene.Indoors[index];
            indoors[index] = new GpuIndoor(indoor.Min, indoor.Ambient, indoor.Max);
        }

        return indoors;
    }

    public void Dispose()
    {
        _frames.Dispose();
    }
}
