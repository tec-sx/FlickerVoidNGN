using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

using Arch.Core;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Draws every entity with a transform and a mesh renderer, seen through the world's camera and lit by a sun, an even ambient
/// light, and point and spot lights sorted into <see cref="LightClusters"/> (clustered forward shading).
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
    private static readonly Vector4 Background = new(0, 0, 0, 1);
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>();
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>();

    private readonly IPipeline _pipeline;
    private readonly ITexture _white;
    private readonly ISampler _sampler;
    private readonly MeshLibrary _meshes;
    private readonly IGraphicsDevice _device;
    private readonly LightClusters _clusters = new();
    private readonly List<GpuLight> _lights = [];
    private readonly List<LightBounds> _lightBounds = [];
    private readonly IBuffer?[] _frames = new IBuffer?[MaxFramesInFlight];
    private int _frameSlot;

    // TODO(Stage 5): load the scene shader's compiled bytecode from the asset pipeline instead of compiling it at startup.
    public SceneRenderer(IGraphicsDevice device, IShaderCompiler compiler, IContentManager content, PixelFormat colorFormat)
    {
        _device = device;
        _pipeline = CreatePipeline(device, compiler, colorFormat);
        TextureDescription whiteDescription = new(1, 1, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead) { Name = "White" };
        _white = device.CreateTexture(whiteDescription, [255, 255, 255, 255]);
        SamplerDescription samplerDescription = new(SamplerFilter.Linear, SamplerAddressMode.Wrap);
        _sampler = device.CreateSampler(samplerDescription);
        _meshes = new MeshLibrary(device, content);
    }

    /// <summary>The direction the sunlight travels, in world space.</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));

    /// <summary>The share of light that reaches every surface, from 0 to 1; the sun lights the rest.</summary>
    public float Ambient { get; set; } = 0.35f;

    /// <summary>Clears both targets and draws the world from its first camera, or nothing without one; leaves the color target in the RenderTarget state.</summary>
    public void Render(ICommandList commands, World world, ITexture colorTarget, ITexture depthTarget)
    {
        int width = colorTarget.Description.Width;
        int height = colorTarget.Description.Height;
        commands.Barrier(colorTarget, ResourceState.RenderTarget);
        commands.Barrier(depthTarget, ResourceState.DepthWrite);
        ColorTarget color = new(colorTarget, LoadAction.Clear, Background);
        DepthTarget depth = new(depthTarget, LoadAction.Clear);
        commands.BeginRendering([color], depth);

        if (TryFindCamera(world, width, height, out Camera camera, out CameraView view))
        {
            IBuffer frame = WriteFrame(world, camera, view, width, height);
            commands.SetViewport(new Viewport(0, 0, width, height));
            commands.SetScissor(0, 0, width, height);
            commands.SetPipeline(_pipeline);
            DrawMeshes(commands, world, frame.BindlessIndex);
        }

        commands.EndRendering();
    }

    public void Dispose()
    {
        foreach (IBuffer? frame in _frames)
        {
            frame?.Dispose();
        }

        _meshes.Dispose();
        _sampler.Dispose();
        _white.Dispose();
        _pipeline.Dispose();
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

    private void GatherLights(World world)
    {
        _lights.Clear();
        _lightBounds.Clear();
        world.Query(in PointLights, (ref PointLight light, ref Transform transform) =>
        {
            Vector3 color = light.Color * light.Intensity;
            _lights.Add(new GpuLight(transform.Position, light.Range, color, -3, -Vector3.UnitZ, -2));
            _lightBounds.Add(new LightBounds(transform.Position, light.Range));
        });
        world.Query(in SpotLights, (ref SpotLight light, ref Transform transform) =>
        {
            Vector3 color = light.Color * light.Intensity;
            Vector3 direction = Vector3.Transform(-Vector3.UnitZ, transform.Rotation);
            float cosOuter = MathF.Cos(float.DegreesToRadians(light.OuterConeAngle));
            float cosInner = MathF.Cos(float.DegreesToRadians(light.InnerConeAngle));
            _lights.Add(new GpuLight(transform.Position, light.Range, color, cosOuter, direction, cosInner));
            _lightBounds.Add(new LightBounds(transform.Position, light.Range));
        });
    }

    // Writes this frame's view, sun, lights and clusters into the next buffer of the ring, growing it when they don't fit.
    private IBuffer WriteFrame(World world, in Camera camera, in CameraView view, int width, int height)
    {
        GatherLights(world);
        ReadOnlySpan<LightBounds> bounds = CollectionsMarshal.AsSpan(_lightBounds);
        _clusters.Build(camera, view, bounds);

        ReadOnlySpan<GpuLight> lights = CollectionsMarshal.AsSpan(_lights);
        ReadOnlySpan<byte> lightBytes = MemoryMarshal.AsBytes(lights);
        ReadOnlySpan<byte> rangeBytes = MemoryMarshal.AsBytes(_clusters.Ranges);
        ReadOnlySpan<byte> indexBytes = MemoryMarshal.AsBytes(_clusters.Indices);
        int lightsOffset = Marshal.SizeOf<FrameData>();
        int clustersOffset = lightsOffset + lightBytes.Length;
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

        FrameData data = new(
            view.ViewProjection,
            depthAxis,
            SunDirection,
            Ambient,
            new Vector2(width, height),
            _clusters.SliceScale,
            _clusters.SliceBias,
            logarithmic,
            (uint)lights.Length,
            (uint)clustersOffset,
            (uint)indicesOffset);

        _frameSlot = (_frameSlot + 1) % MaxFramesInFlight;
        IBuffer frame = GetFrameBuffer(_frameSlot, size);
        ReadOnlySpan<FrameData> header = new(in data);
        frame.Write(MemoryMarshal.AsBytes(header));
        frame.Write(lightBytes, lightsOffset);
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

    private void DrawMeshes(ICommandList commands, World world, uint frame)
    {
        world.Query(in Meshes, (ref MeshRenderer renderer, ref Transform transform) =>
        {
            Matrix4x4 scale = Matrix4x4.CreateScale(transform.Scale);
            Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(transform.Rotation);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(transform.Position);
            Matrix4x4 meshWorld = scale * rotation * translation;

            IReadOnlyList<GpuMeshPart> parts = _meshes.Get(renderer.Mesh);
            foreach (GpuMeshPart part in parts)
            {
                ITexture texture = part.Texture ?? _white;
                SceneConstants constants = new(meshWorld, part.BaseColor, frame, part.Vertices.BindlessIndex, texture.BindlessIndex, _sampler.BindlessIndex);
                commands.SetIndexBuffer(part.Indices, IndexFormat.UInt32);
                commands.PushConstants(constants);
                commands.DrawIndexed(part.IndexCount);
            }
        });
    }

    private static IPipeline CreatePipeline(IGraphicsDevice device, IShaderCompiler compiler, PixelFormat colorFormat)
    {
        string source = ReadShader();
        GraphicsPipelineDescription description = new()
        {
            VertexShader = Compile(compiler, source, "VertexMain", ShaderStage.Vertex),
            PixelShader = Compile(compiler, source, "PixelMain", ShaderStage.Pixel),
            ColorFormats = [colorFormat],
            DepthFormat = DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = "Scene",
        };
        return device.CreateGraphicsPipeline(description);
    }

    private static string ReadShader()
    {
        Assembly assembly = typeof(SceneRenderer).Assembly;
        using Stream stream = assembly.GetManifestResourceStream($"FlickerVoid.Engine.Rendering.Shaders.{ShaderName}")
            ?? throw new InvalidOperationException($"{ShaderName} isn't embedded in {assembly.GetName().Name}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static ShaderBytecode Compile(IShaderCompiler compiler, string source, string entryPoint, ShaderStage stage)
    {
        ShaderSource shader = new(source, entryPoint, stage) { Name = ShaderName };
        ShaderCompileResult result = compiler.Compile(shader);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{ShaderName} {entryPoint} failed to compile:\n{result.Messages}");
        }

        return new ShaderBytecode(result.Bytecode, entryPoint);
    }
}
