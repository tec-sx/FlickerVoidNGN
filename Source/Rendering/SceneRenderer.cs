using System.Numerics;
using System.Runtime.InteropServices;

using Arch.Core;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Draws every entity with a transform and a mesh renderer, seen through the world's camera and lit by a sun, an even ambient
/// light, and point and spot lights sorted into <see cref="LightClusters"/> (clustered forward shading). The sun and the
/// lights that cast shadows draw their depth into the <see cref="ShadowAtlas"/> first. Inside the first
/// <see cref="IrradianceVolume"/>, its baked probes add the light bounced off the scene. A depth prepass gives ambient
/// occlusion and contact shadows, and the lit scene is drawn in HDR and graded into the output as the <see cref="Mood"/> says.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    public const PixelFormat DepthFormat = PixelFormat.Depth32Float;

    /// <summary>
    /// How many frames the GPU may still be drawing when <see cref="Render"/> is called: each frame writes its lights to its own
    /// buffer, reused this many frames later.
    /// </summary>
    public const int MaxFramesInFlight = 3;

    private const string ShaderName = "Scene.hlsl";
    private const string FrameShader = "Frame.hlsl";
    private const int GroupSize = 8;

    // How far beyond the view toward the sun shadow casters are still drawn, in world units.
    private const float SunCasterReach = 50;

    // Texels between a filtered shadow lookup and its tile's edge, so filtering never reads a neighbouring tile.
    private const float TileInset = 2;

    private static readonly Vector4 Background = new(0, 0, 0, 1);
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>();
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>();
    private static readonly QueryDescription Volumes = new QueryDescription().WithAll<IrradianceVolume, Transform>();

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _pipeline;
    private readonly IPipeline _depthPipeline;
    private readonly IPipeline _screenShadowPipeline;
    private readonly ScreenTargets _targets;
    private readonly PostProcessor _post;
    private readonly ITexture _white;
    private readonly ISampler _sampler;
    private readonly ISampler _clampSampler;
    private readonly MeshLibrary _meshes;
    private readonly TextureLibrary _textures;
    private readonly ShadowRenderer _shadows;
    private readonly ShadowAtlas _atlas = new();
    private readonly LightClusters _clusters = new();
    private readonly List<MeshDraw> _draws = [];
    private readonly List<GpuLight> _lights = [];
    private readonly List<LightBounds> _lightBounds = [];
    private readonly List<GpuShadow> _gpuShadows = [];
    private readonly List<ShadowView> _shadowViews = [];
    private readonly IBuffer?[] _frames = new IBuffer?[MaxFramesInFlight];
    private readonly Dictionary<string, IrradianceProbes> _loadedProbes = [];
    private readonly IContentManager _content;
    private IrradianceProbes? _uploadedProbes;
    private IBuffer? _probeBuffer;
    private int _frameSlot;
    private uint _sunShadow;

    public SceneRenderer(IGraphicsDevice device, IShaderCompiler compiler, IContentManager content, PixelFormat colorFormat)
    {
        _device = device;
        _content = content;
        _pipeline = CreatePipeline(device, compiler);
        _depthPipeline = CreateDepthPipeline(device, compiler);
        ComputePipelineDescription screenShadows = new()
        {
            ComputeShader = EmbeddedShaders.Compile(compiler, "ScreenShadows.hlsl", "ComputeMain", ShaderStage.Compute, FrameShader),
            Name = "Screen shadows",
        };
        _screenShadowPipeline = device.CreateComputePipeline(screenShadows);
        _targets = new ScreenTargets(device);
        _post = new PostProcessor(device, compiler, colorFormat);
        TextureDescription whiteDescription = new(1, 1, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead) { Name = "White" };
        _white = device.CreateTexture(whiteDescription, [255, 255, 255, 255]);
        SamplerDescription samplerDescription = new(SamplerFilter.Linear, SamplerAddressMode.Wrap);
        _sampler = device.CreateSampler(samplerDescription);
        SamplerDescription clampDescription = new(SamplerFilter.Linear, SamplerAddressMode.Clamp);
        _clampSampler = device.CreateSampler(clampDescription);
        _meshes = new MeshLibrary(device, content);
        _textures = new TextureLibrary(device, content);
        _shadows = new ShadowRenderer(device, compiler);
    }

    /// <summary>How the scene is lit and graded; replace it to change the mood.</summary>
    public LightingMood Mood { get; set; } = new();

    /// <summary>Probes that replace the world's irradiance volume while a bake renders, or null.</summary>
    internal ProbeSource? BakeProbes { get; set; }

    /// <summary>Draws the world from its first camera into the color target, or clears it without one; leaves it in the RenderTarget state.</summary>
    public void Render(ICommandList commands, World world, ITexture colorTarget)
    {
        int width = colorTarget.Description.Width;
        int height = colorTarget.Description.Height;
        if (!TryFindCamera(world, width, height, out Camera camera, out CameraView view))
        {
            commands.Barrier(colorTarget, ResourceState.RenderTarget);
            ColorTarget clear = new(colorTarget, LoadAction.Clear, Background);
            commands.BeginRendering([clear]);
            commands.EndRendering();
            return;
        }

        RenderView(commands, world, colorTarget, camera, view);
    }

    /// <summary>Draws the world through the given camera into the color target; leaves it in the RenderTarget state.</summary>
    public void Render(ICommandList commands, World world, ITexture colorTarget, in Camera camera, in Transform cameraTransform)
    {
        int width = colorTarget.Description.Width;
        int height = colorTarget.Description.Height;
        CameraView view = CameraView.Create(camera, cameraTransform, width, height);
        RenderView(commands, world, colorTarget, camera, view);
    }

    public void Dispose()
    {
        foreach (IBuffer? frame in _frames)
        {
            frame?.Dispose();
        }

        _probeBuffer?.Dispose();
        _post.Dispose();
        _targets.Dispose();
        _screenShadowPipeline.Dispose();
        _depthPipeline.Dispose();
        _shadows.Dispose();
        _textures.Dispose();
        _meshes.Dispose();
        _clampSampler.Dispose();
        _sampler.Dispose();
        _white.Dispose();
        _pipeline.Dispose();
    }

    // Shadows, then a depth prepass the screen shadows are worked out from, then the lit scene, then post-processing.
    private void RenderView(ICommandList commands, World world, ITexture colorTarget, in Camera camera, in CameraView view)
    {
        int width = colorTarget.Description.Width;
        int height = colorTarget.Description.Height;
        _targets.Ensure(width, height);
        CollectDraws(world);
        GatherLights(world, camera, view);
        _shadows.Draw(commands, _shadowViews, _draws);
        ProbeSource? probes = FindProbes(world);
        IBuffer frame = WriteFrame(camera, view, width, height, probes);
        Viewport viewport = new(0, 0, width, height);

        commands.Barrier(_targets.Depth, ResourceState.DepthWrite);
        DepthTarget clearDepth = new(_targets.Depth, LoadAction.Clear);
        commands.BeginRendering([], clearDepth);
        commands.SetViewport(viewport);
        commands.SetScissor(0, 0, width, height);
        commands.SetPipeline(_depthPipeline);
        DrawMeshes(commands, frame.BindlessIndex);
        commands.EndRendering();

        commands.Barrier(_targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(_targets.ScreenShadows, ResourceState.ShaderWrite);
        commands.SetPipeline(_screenShadowPipeline);
        ScreenShadowConstants screenShadows = new(frame.BindlessIndex, _targets.Depth.BindlessIndex, _targets.ScreenShadows.BindlessIndex);
        commands.PushConstants(screenShadows);
        commands.Dispatch((width + GroupSize - 1) / GroupSize, (height + GroupSize - 1) / GroupSize);
        commands.Barrier(_targets.ScreenShadows, ResourceState.ShaderRead);

        commands.Barrier(_targets.Depth, ResourceState.DepthWrite);
        commands.Barrier(_targets.Hdr, ResourceState.RenderTarget);
        ColorTarget hdr = new(_targets.Hdr, LoadAction.Clear, Background);
        DepthTarget depth = new(_targets.Depth, LoadAction.Load);
        commands.BeginRendering([hdr], depth);
        commands.SetViewport(viewport);
        commands.SetScissor(0, 0, width, height);
        commands.SetPipeline(_pipeline);
        DrawMeshes(commands, frame.BindlessIndex);
        commands.EndRendering();

        ITexture? paper = null;
        if (Mood.Paper is not null)
        {
            paper = _textures.Get(Mood.Paper);
        }

        _post.Run(commands, _targets, colorTarget, Mood, paper, _clampSampler, _sampler);
    }

    private ProbeSource? FindProbes(World world)
    {
        if (BakeProbes is not null)
        {
            return BakeProbes;
        }

        ProbeSource? found = null;
        world.Query(in Volumes, (ref IrradianceVolume volume, ref Transform transform) =>
        {
            if (found is null && volume.Probes is not null)
            {
                IrradianceProbes probes = LoadProbes(volume);
                found = new ProbeSource(volume, transform.Position, probes);
            }
        });

        return found;
    }

    private IrradianceProbes LoadProbes(in IrradianceVolume volume)
    {
        string path = volume.Probes!;
        string fullPath = _content.GetFullPath(path);
        if (!_loadedProbes.TryGetValue(fullPath, out IrradianceProbes? probes))
        {
            using FileStream stream = File.OpenRead(fullPath);
            probes = IrradianceProbes.Load(stream);
            _loadedProbes.Add(fullPath, probes);
        }

        bool matches = probes.CountX == volume.ProbesX && probes.CountY == volume.ProbesY && probes.CountZ == volume.ProbesZ;
        if (!matches)
        {
            throw new InvalidDataException($"{path} holds {probes.CountX}x{probes.CountY}x{probes.CountZ} probes, but its volume has {volume.ProbesX}x{volume.ProbesY}x{volume.ProbesZ}; bake it again.");
        }

        return probes;
    }

    // Uploads the probes when they change; the device releases the old buffer once the GPU is done with it.
    private uint UploadProbes(IrradianceProbes probes)
    {
        if (!ReferenceEquals(probes, _uploadedProbes))
        {
            _probeBuffer?.Dispose();
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes<IrradianceProbe>(probes.Probes);
            BufferDescription description = new(bytes.Length, BufferUsage.ShaderRead) { Name = "Irradiance probes" };
            _probeBuffer = _device.CreateBuffer(description, bytes);
            _uploadedProbes = probes;
        }

        return _probeBuffer!.BindlessIndex;
    }

    private static bool TryFindCamera(World world, int width, int height, out Camera camera, out CameraView view)
    {
        bool found = false;
        Camera firstCamera = default;
        CameraView firstView = default;
        world.Query(in Cameras, (ref Camera candidate, ref Transform transform) =>
        {
            if (!found)
            {
                firstCamera = candidate;
                firstView = CameraView.Create(candidate, transform, width, height);
                found = true;
            }
        });

        camera = firstCamera;
        view = firstView;
        return found;
    }

    private void CollectDraws(World world)
    {
        _draws.Clear();
        world.Query(in Meshes, (ref MeshRenderer renderer, ref Transform transform) =>
        {
            Matrix4x4 scale = Matrix4x4.CreateScale(transform.Scale);
            Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(transform.Rotation);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(transform.Position);
            Matrix4x4 meshWorld = scale * rotation * translation;
            IReadOnlyList<GpuMeshPart> parts = _meshes.Get(renderer.Mesh);
            foreach (GpuMeshPart part in parts)
            {
                _draws.Add(new MeshDraw(meshWorld, part));
            }
        });
    }

    // Gathers the lights and hands out shadow atlas tiles: the sun first, then lights in query order while tiles last.
    private void GatherLights(World world, in Camera camera, in CameraView view)
    {
        _lights.Clear();
        _lightBounds.Clear();
        _gpuShadows.Clear();
        _shadowViews.Clear();
        _atlas.Reset();

        _sunShadow = GpuLight.None;
        LightingMood mood = Mood;
        if (mood.SunShadowDistance > 0)
        {
            Vector3 sunDirection = Vector3.Normalize(mood.SunDirection);
            Matrix4x4 sun = ShadowProjections.FitSun(camera, view, sunDirection, mood.SunShadowDistance, SunCasterReach);
            _sunShadow = AddShadow(sun, _atlas.Sun, orthographic: true);
        }

        world.Query(in PointLights, (ref PointLight light, ref Transform transform) =>
        {
            uint shadow = GpuLight.None;
            if (light.CastsShadows && _atlas.FreeTiles >= ShadowProjections.PointFaces)
            {
                shadow = (uint)_gpuShadows.Count;
                for (int face = 0; face < ShadowProjections.PointFaces; face++)
                {
                    _atlas.TryAllocate(out ShadowTile tile);
                    Matrix4x4 faceView = ShadowProjections.PointFace(transform.Position, face, light.Range);
                    AddShadow(faceView, tile, orthographic: false);
                }
            }

            Vector3 color = light.Color * light.Intensity;
            _lights.Add(new GpuLight(transform.Position, light.Range, color, -3, -Vector3.UnitZ, -2, Vector3.UnitX, GpuLight.None, shadow));
            _lightBounds.Add(new LightBounds(transform.Position, light.Range));
        });
        world.Query(in SpotLights, (ref SpotLight light, ref Transform transform) =>
        {
            uint shadow = GpuLight.None;
            if (light.CastsShadows && _atlas.TryAllocate(out ShadowTile tile))
            {
                Matrix4x4 spotView = ShadowProjections.Spot(transform.Position, transform.Rotation, light.OuterConeAngle, light.Range);
                shadow = AddShadow(spotView, tile, orthographic: false);
            }

            uint cookie = GpuLight.None;
            if (light.Cookie is not null)
            {
                ITexture texture = _textures.Get(light.Cookie);
                cookie = texture.BindlessIndex;
            }

            Vector3 color = light.Color * light.Intensity;
            Vector3 direction = Vector3.Transform(-Vector3.UnitZ, transform.Rotation);
            Vector3 right = Vector3.Transform(Vector3.UnitX, transform.Rotation);
            float cosOuter = MathF.Cos(float.DegreesToRadians(light.OuterConeAngle));
            float cosInner = MathF.Cos(float.DegreesToRadians(light.InnerConeAngle));
            _lights.Add(new GpuLight(transform.Position, light.Range, color, cosOuter, direction, cosInner, right, cookie, shadow));
            _lightBounds.Add(new LightBounds(transform.Position, light.Range));
        });
    }

    private uint AddShadow(Matrix4x4 viewProjection, ShadowTile tile, bool orthographic)
    {
        _shadowViews.Add(new ShadowView(viewProjection, tile));
        Matrix4x4 toTile = ShadowProjections.ToAtlasTile(tile);
        Matrix4x4 worldToAtlas = viewProjection * toTile;
        float inset = TileInset / ShadowAtlas.Size;
        float start = (float)tile.X / ShadowAtlas.Size;
        float top = (float)tile.Y / ShadowAtlas.Size;
        float size = (float)tile.Size / ShadowAtlas.Size;
        Vector4 rect = new(start + inset, top + inset, start + size - inset, top + size - inset);

        // The projection's x scale is 1 / half the view's width (orthographic) or 1 / tan(half the field of view) (perspective).
        Vector3 xAxis = new(viewProjection.M11, viewProjection.M21, viewProjection.M31);
        float texel = 2 / (xAxis.Length() * tile.Size);
        GpuShadow shadow;
        if (orthographic)
        {
            shadow = new GpuShadow(worldToAtlas, rect, 0, texel);
        }
        else
        {
            shadow = new GpuShadow(worldToAtlas, rect, texel, 0);
        }

        _gpuShadows.Add(shadow);
        return (uint)(_gpuShadows.Count - 1);
    }

    // Writes this frame's view, sun, lights, shadows and clusters into the next buffer of the ring, growing it when they don't fit.
    private IBuffer WriteFrame(in Camera camera, in CameraView view, int width, int height, ProbeSource? probes)
    {
        ReadOnlySpan<LightBounds> bounds = CollectionsMarshal.AsSpan(_lightBounds);
        _clusters.Build(camera, view, bounds);

        ReadOnlySpan<byte> lightBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(_lights));
        ReadOnlySpan<byte> shadowBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(_gpuShadows));
        ReadOnlySpan<byte> rangeBytes = MemoryMarshal.AsBytes(_clusters.Ranges);
        ReadOnlySpan<byte> indexBytes = MemoryMarshal.AsBytes(_clusters.Indices);
        int lightsOffset = Marshal.SizeOf<FrameData>();
        int shadowsOffset = lightsOffset + lightBytes.Length;
        int clustersOffset = shadowsOffset + shadowBytes.Length;
        int indicesOffset = clustersOffset + rangeBytes.Length;
        int size = indicesOffset + indexBytes.Length;

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
        if (probes?.Probes is not null)
        {
            probeBuffer = UploadProbes(probes.Probes);
            volume = probes.Volume;
            probeSize = volume.Size;
            probeMin = probes.Center - (volume.Size / 2);
        }

        Matrix4x4.Invert(viewMatrix, out Matrix4x4 cameraWorld);
        Matrix4x4.Invert(view.ViewProjection, out Matrix4x4 inverseViewProjection);
        LightingMood mood = Mood;
        Vector3 sunDirection = Vector3.Normalize(mood.SunDirection);
        FrameData data = new(
            view.ViewProjection,
            depthAxis,
            sunDirection,
            mood.Ambient,
            new Vector2(width, height),
            _clusters.SliceScale,
            _clusters.SliceBias,
            logarithmic,
            (uint)_lights.Count,
            (uint)clustersOffset,
            (uint)indicesOffset,
            (uint)shadowsOffset,
            _sunShadow,
            _shadows.Atlas.BindlessIndex,
            _clampSampler.BindlessIndex,
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
            _targets.ScreenShadows.BindlessIndex,
            mood.AmbientOcclusion,
            mood.AmbientOcclusionRadius,
            mood.ContactShadowLength);

        _frameSlot = (_frameSlot + 1) % MaxFramesInFlight;
        IBuffer frame = GetFrameBuffer(_frameSlot, size);
        ReadOnlySpan<FrameData> header = new(in data);
        frame.Write(MemoryMarshal.AsBytes(header));
        frame.Write(lightBytes, lightsOffset);
        frame.Write(shadowBytes, shadowsOffset);
        frame.Write(rangeBytes, clustersOffset);
        frame.Write(indexBytes, indicesOffset);
        return frame;
    }

    private IBuffer GetFrameBuffer(int slot, int size)
    {
        IBuffer? frame = _frames[slot];
        if (frame is not null && frame.Description.Size >= size)
        {
            return frame;
        }

        // The device releases a disposed buffer only after the GPU finishes the work already submitted, which may still read it.
        frame?.Dispose();
        long capacity = (long)BitOperations.RoundUpToPowerOf2((uint)size);
        BufferDescription description = new(capacity, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = $"Frame {slot}" };
        frame = _device.CreateBuffer(description);
        _frames[slot] = frame;
        return frame;
    }

    private void DrawMeshes(ICommandList commands, uint frame)
    {
        foreach (MeshDraw draw in _draws)
        {
            GpuMeshPart part = draw.Part;
            ITexture texture = part.Texture ?? _white;
            SceneConstants constants = new(draw.World, part.BaseColor, frame, part.Vertices.BindlessIndex, texture.BindlessIndex, _sampler.BindlessIndex);
            commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
            commands.PushConstants(constants);
            commands.DrawIndexed(part.IndexCount);
        }
    }

    private static IPipeline CreatePipeline(IGraphicsDevice device, IShaderCompiler compiler)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = EmbeddedShaders.Compile(compiler, ShaderName, "VertexMain", ShaderStage.Vertex, FrameShader),
            PixelShader = EmbeddedShaders.Compile(compiler, ShaderName, "PixelMain", ShaderStage.Pixel, FrameShader),
            ColorFormats = [ScreenTargets.HdrFormat],
            DepthFormat = DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = "Scene",
        };
        return device.CreateGraphicsPipeline(description);
    }

    private static IPipeline CreateDepthPipeline(IGraphicsDevice device, IShaderCompiler compiler)
    {
        GraphicsPipelineDescription description = new()
        {
            VertexShader = EmbeddedShaders.Compile(compiler, ShaderName, "VertexMain", ShaderStage.Vertex, FrameShader),
            PixelShader = EmbeddedShaders.Compile(compiler, ShaderName, "DepthMain", ShaderStage.Pixel, FrameShader),
            ColorFormats = [],
            DepthFormat = DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = "Depth prepass",
        };
        return device.CreateGraphicsPipeline(description);
    }
}
