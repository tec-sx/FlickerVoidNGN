using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Resources;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Instancing;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Writes the frame's view (jittered for an upscaler), sun, lights, shadows, light clusters, probes, indoor volumes, cutaway, draws
/// and the bindless indices of its traced shadows and occlusion, depth, G-buffer and acceleration structure into the frame buffer
/// ring, after every pass has prepared and before any executes; remembers the view without its jitter, and the jitter, for the
/// next frame's view before, which a frame that starts its history afresh takes from its own.
/// </summary>
internal sealed class FrameDataWriter : IDisposable
{
    // Draw records start on a 16-byte boundary, as their matrices and vectors are read in fours.
    private const int DrawAlignment = 16;

    private readonly LightClusters _clusters = new();
    private readonly FrameBufferRing _frames;
    private readonly ProbeCache _probes;
    private readonly List<GpuDraw> _draws = [];
    private Matrix4x4 _previousViewProjection;
    private Vector2 _previousJitter;
    private Vector4 _previousDepthAxis;
    private uint _number;

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

        Matrix4x4 viewMatrix = view.View;
        Vector4 depthAxis = view.DepthAxis;
        Vector3 viewDirection = new(depthAxis.X, depthAxis.Y, depthAxis.Z);
        CollectDraws(context, viewDirection);
        ReadOnlySpan<byte> drawBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(_draws));
        int drawsOffset = AlignUp(indoorsOffset + indoorBytes.Length, DrawAlignment);
        int size = drawsOffset + drawBytes.Length;
        uint logarithmic = 0;
        if (_clusters.LogarithmicSlices)
        {
            logarithmic = 1;
        }

        uint probeBuffer = GpuLight.None;
        Vector3 probeMin = Vector3.Zero;
        Vector3 probeSize = Vector3.Zero;
        IrradianceVolume volume = new();
        if (context.ProbeSource is { } probes && FindProbeBuffer(probes) is { } found)
        {
            probeBuffer = found;
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
        CameraView jittered = view.Jittered(context.Jitter, context.RenderWidth, context.RenderHeight);
        Matrix4x4 viewProjection = jittered.ViewProjection;
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection);
        LightingMood mood = context.Mood;
        DepthFade fade = context.DepthFade;
        float focusDepth = 0;
        if (context.Focus is { } focus)
        {
            focusDepth = Vector4.Dot(new Vector4(focus, 1), depthAxis);
        }

        if (context.ResetsHistory)
        {
            _previousViewProjection = view.ViewProjection;
            _previousJitter = context.Jitter;
            _previousDepthAxis = depthAxis;
        }

        Matrix4x4 previousViewProjection = _previousViewProjection;
        Vector2 previousJitter = _previousJitter;
        Vector4 previousDepthAxis = _previousDepthAxis;
        _previousDepthAxis = depthAxis;
        _previousViewProjection = view.ViewProjection;
        _previousJitter = context.Jitter;
        DirectionalLight directional = DirectionalLight.Of(mood);
        Vector3 sunDirection = Vector3.Normalize(directional.Direction);
        Vector3 sunLight = directional.Light;
        GBuffer gBuffer = context.GBuffer;
        uint accelerationStructure = BindlessIndex.None;
        if (context.AccelerationStructure is { } scene)
        {
            accelerationStructure = scene.BindlessIndex;
        }

        uint reflection = GpuLight.None;
        if (mood.Reflection is not null)
        {
            ITexture image = context.Textures.Get(mood.Reflection);
            reflection = image.BindlessIndex;
        }

        uint rayShadows = GpuLight.None;
        if (context.RayShadows is { } tracedShadows)
        {
            rayShadows = tracedShadows.BindlessIndex;
        }

        uint rayOcclusion = GpuLight.None;
        if (context.RayOcclusion is { } tracedOcclusion)
        {
            rayOcclusion = tracedOcclusion.BindlessIndex;
        }

        uint fogVolume = GpuLight.None;
        if (context.FogVolume is { } volumeBuffer)
        {
            fogVolume = volumeBuffer.BindlessIndex;
        }

        Vector3 hazeColor = Vector3.Lerp(mood.HazeColor, mood.SkyHorizon, mood.HazeFromSky);

        // Ray Reconstruction asks for samples that hold no pattern, so a denoised frame's noise is drawn afresh by its number.
        uint noiseFrame = GpuLight.None;
        if (context.Denoises)
        {
            noiseFrame = _number;
        }

        _number++;

        FrameData data = new(
            viewProjection,
            depthAxis,
            sunDirection,
            mood.Ambient,
            new Vector2(context.RenderWidth, context.RenderHeight),
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
            context.Jitter,
            cameraWorld.Translation,
            camera.NearPlane,
            mood.FogColor,
            mood.FogDensity,
            mood.FogAnisotropy,
            mood.FogHeight,
            mood.FogHeightFalloff,
            noiseFrame,
            inverseViewProjection,
            context.Targets.ScreenShadows.BindlessIndex,
            mood.AmbientOcclusion,
            mood.AmbientOcclusionRadius,
            mood.ContactShadowLength,
            focusDepth,
            fade.NearStart,
            fade.NearEnd,
            fade.NearAmount,
            fade.NearBurn,
            Vector2.Zero,
            fade.FarStart,
            fade.FarEnd,
            fade.FarAmount,
            previousJitter,
            cutaway.Min,
            cut,
            cutaway.Max,
            cutaway.OutsideMuted,
            (uint)indoors.Length,
            (uint)indoorsOffset,
            rayShadows,
            rayOcclusion,
            previousViewProjection,
            (uint)drawsOffset,
            context.Targets.Depth.BindlessIndex,
            gBuffer.Albedo.BindlessIndex,
            gBuffer.Normal.BindlessIndex,
            gBuffer.Material.BindlessIndex,
            gBuffer.ObjectDepth.BindlessIndex,
            gBuffer.Motion.BindlessIndex,
            accelerationStructure,
            sunLight,
            0,
            hazeColor,
            mood.HazeDensity,
            mood.HazeStart,
            mood.HazeMax,
            mood.HazeHeight,
            mood.HazeHeightFalloff,
            previousDepthAxis,
            mood.FogRange,
            fogVolume,
            mood.Rain,
            0,
            context.Targets.Behind.BindlessIndex,
            reflection,
            context.Sampler.BindlessIndex,
            mood.ReflectionIntensity,
            mood.ShadowColor,
            mood.ShadowDarkness);

        IBuffer frame = _frames.Get(size);
        ReadOnlySpan<FrameData> header = new(in data);
        frame.Write(MemoryMarshal.AsBytes(header));
        frame.Write(lightBytes, lightsOffset);
        frame.Write(shadowBytes, shadowsOffset);
        frame.Write(rangeBytes, clustersOffset);
        frame.Write(indexBytes, indicesOffset);
        frame.Write(indoorBytes, indoorsOffset);
        frame.Write(drawBytes, drawsOffset);
        return frame;
    }

    /// <summary>The bindless index of the buffer holding the probes: the live ones traced on the GPU, else the baked ones uploaded.</summary>
    private uint? FindProbeBuffer(ProbeSource probes)
    {
        if (probes.Live is { } live)
        {
            return live.BindlessIndex;
        }

        if (probes.Probes is { } baked)
        {
            return _probes.Upload(baked);
        }

        return null;
    }

    private static int AlignUp(int offset, int alignment)
    {
        return (offset + alignment - 1) / alignment * alignment;
    }

    /// <summary>
    /// A record for every draw, in the order of the context's draws, so a draw's number is its place there. A posed part's box is
    /// its rest pose's, placed where its mesh stands.
    /// </summary>
    private void CollectDraws(RenderContext context, Vector3 viewDirection)
    {
        _draws.Clear();
        foreach (MeshDraw draw in context.Draws)
        {
            GpuMeshPart part = draw.Part;
            ITexture texture = part.Texture ?? context.White;
            Vector4 outlineColor = Vector4.Zero;
            if (draw.Outline is { } outline)
            {
                outlineColor = new Vector4(outline.Color, 1);
            }

            Bounds box = draw.Bounds ?? part.Bounds.Transform(draw.World);
            uint scales = 0;
            if (draw.Instanced is { } instanced)
            {
                // Each copy places the part's own box, so its centre stays the model's and the shader moves it.
                InstanceBatch batch = instanced.Batch;
                scales = batch.PackScales();
                float middle = (batch.MinScale + batch.MaxScale) / 2;
                Matrix4x4 sized = Matrix4x4.CreateScale(middle);
                box = part.Bounds.Transform(sized);
            }

            float wholeness = ObjectDepth.Wholeness(box, viewDirection);

            GpuDraw record = new(
                draw.World,
                draw.PreviousWorld,
                part.BaseColor,
                outlineColor,
                draw.Vertices.BindlessIndex,
                draw.PreviousVertices.BindlessIndex,
                texture.BindlessIndex,
                context.MaterialSampler.BindlessIndex,
                draw.Material.Parameters.BindlessIndex,
                (uint)draw.Flags,
                part.Indices.BindlessIndex,
                scales,
                box.Center,
                wholeness);
            _draws.Add(record);
        }
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
