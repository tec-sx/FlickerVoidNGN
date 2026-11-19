using System.Numerics;
using System.Reflection;

using Arch.Core;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Draws every entity with a transform and a mesh renderer, seen through the world's camera and lit by a sun and an even ambient light.</summary>
public sealed class SceneRenderer : IDisposable
{
    public const PixelFormat DepthFormat = PixelFormat.Depth32Float;

    private const string ShaderName = "Scene.hlsl";
    private static readonly Vector4 Background = new(0, 0, 0, 1);
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>();

    private readonly IPipeline _pipeline;
    private readonly ITexture _white;
    private readonly ISampler _sampler;
    private readonly MeshLibrary _meshes;

    // TODO(Stage 5): load the scene shader's compiled bytecode from the asset pipeline instead of compiling it at startup.
    public SceneRenderer(IGraphicsDevice device, IShaderCompiler compiler, IContentManager content, PixelFormat colorFormat)
    {
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

        if (TryFindCamera(world, width, height, out CameraView view))
        {
            commands.SetViewport(new Viewport(0, 0, width, height));
            commands.SetScissor(0, 0, width, height);
            commands.SetPipeline(_pipeline);
            DrawMeshes(commands, world, view.ViewProjection);
        }

        commands.EndRendering();
    }

    public void Dispose()
    {
        _meshes.Dispose();
        _sampler.Dispose();
        _white.Dispose();
        _pipeline.Dispose();
    }

    private static bool TryFindCamera(World world, int width, int height, out CameraView view)
    {
        bool found = false;
        CameraView first = default;
        world.Query(in Cameras, (ref Camera camera, ref Transform transform) =>
        {
            if (!found)
            {
                first = CameraView.Create(camera, transform, width, height);
                found = true;
            }
        });

        view = first;
        return found;
    }

    private void DrawMeshes(ICommandList commands, World world, Matrix4x4 viewProjection)
    {
        world.Query(in Meshes, (ref MeshRenderer renderer, ref Transform transform) =>
        {
            Matrix4x4 scale = Matrix4x4.CreateScale(transform.Scale);
            Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(transform.Rotation);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(transform.Position);
            Matrix4x4 meshWorld = scale * rotation * translation;
            Matrix4x4 worldViewProjection = meshWorld * viewProjection;
            Matrix4x4.Invert(meshWorld, out Matrix4x4 toMesh);
            Vector3 sun = Vector3.TransformNormal(SunDirection, toMesh);

            IReadOnlyList<GpuMeshPart> parts = _meshes.Get(renderer.Mesh);
            foreach (GpuMeshPart part in parts)
            {
                ITexture texture = part.Texture ?? _white;
                SceneConstants constants = new(worldViewProjection, sun, part.Vertices.BindlessIndex, texture.BindlessIndex, _sampler.BindlessIndex, Ambient, 0, part.BaseColor);
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
