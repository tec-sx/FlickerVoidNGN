using System.Numerics;
using System.Runtime.InteropServices;

using Arch.System;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Shaders;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>
/// Renders a turning textured cube to the window through an orthographic camera each frame, following the window's size.
/// Escape or the frame limit closes the window.
/// </summary>
public sealed class MeshRenderSystem : ISystem<GameTime>
{
    private const int FramesInFlight = 3;
    private const float ViewHeight = 2.6f;
    private const float RadiansPerSecond = 0.6f;
    private const PixelFormat DepthFormat = PixelFormat.Depth32Float;

    private static readonly Vector4 Background = new(0.11f, 0.10f, 0.13f, 1);
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));
    private static readonly Vector3 Eye = new(3, 2.4f, 3);

    private readonly IGameWindow _window;
    private readonly IGraphicsDevice _device;
    private readonly int _frameLimit;
    private readonly ISwapchain _swapchain;
    private readonly IPipeline _pipeline;
    private readonly IBuffer _vertices;
    private readonly IBuffer _indices;
    private readonly int _indexCount;
    private readonly ITexture _texture;
    private readonly ISampler _sampler;
    private readonly ICommandList[] _commandLists = new ICommandList[FramesInFlight];
    private ITexture _depth;
    private float _angle;
    private bool _disposed;

    public MeshRenderSystem(IGameWindow window, IGraphicsDevice device, IShaderCompiler compiler, int frameLimit)
    {
        _window = window;
        _device = device;
        _frameLimit = frameLimit;
        Vector2D<int> size = window.Size;
        SwapchainDescription swapchainDescription = new(window.Native, size.X, size.Y) { Name = "Window" };
        _swapchain = device.CreateSwapchain(swapchainDescription);
        _depth = CreateDepth();
        _pipeline = CreatePipeline(compiler);

        byte[] vertexData = Cube.CreateVertices();
        ushort[] indices = Cube.CreateIndices();
        byte[] indexData = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        BufferDescription vertexDescription = new(vertexData.Length, BufferUsage.ShaderRead) { Name = "Cube vertices" };
        BufferDescription indexDescription = new(indexData.Length, BufferUsage.Index) { Name = "Cube indices" };
        _vertices = device.CreateBuffer(vertexDescription, vertexData);
        _indices = device.CreateBuffer(indexDescription, indexData);
        _indexCount = indices.Length;

        byte[] texels = PaintedTexture.Create();
        TextureDescription textureDescription = new(PaintedTexture.Size, PaintedTexture.Size, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead) { Name = "Painted" };
        _texture = device.CreateTexture(textureDescription, texels);
        SamplerDescription samplerDescription = new(SamplerFilter.Point, SamplerAddressMode.Clamp);
        _sampler = device.CreateSampler(samplerDescription);

        for (int index = 0; index < FramesInFlight; index++)
        {
            _commandLists[index] = device.CreateCommandList();
        }
    }

    public int Frames { get; private set; }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        Frames++;
        _angle += (float)t.Elapsed.TotalSeconds * RadiansPerSecond;
        Vector2D<int> size = _window.Size;
        bool visible = size.X > 0 && size.Y > 0;
        if (visible && (size.X != _swapchain.Width || size.Y != _swapchain.Height))
        {
            _swapchain.Resize(size.X, size.Y);
            _depth.Dispose();
            _depth = CreateDepth();
        }

        if (visible)
        {
            Render();
        }

        bool limitReached = _frameLimit > 0 && Frames >= _frameLimit;
        if (_window.Input.WasPressed(Key.Escape) || limitReached)
        {
            _window.Close();
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _device.WaitIdle();
        foreach (ICommandList commands in _commandLists)
        {
            commands.Dispose();
        }

        _sampler.Dispose();
        _texture.Dispose();
        _indices.Dispose();
        _vertices.Dispose();
        _pipeline.Dispose();
        _depth.Dispose();
        _swapchain.Dispose();
    }

    private void Render()
    {
        ITexture backBuffer = _swapchain.AcquireNextTexture();
        ICommandList commands = _commandLists[Frames % FramesInFlight];
        commands.Begin();
        commands.Barrier(backBuffer, ResourceState.RenderTarget);
        commands.Barrier(_depth, ResourceState.DepthWrite);
        ColorTarget color = new(backBuffer, LoadAction.Clear, Background);
        DepthTarget depth = new(_depth, LoadAction.Clear);
        commands.BeginRendering([color], depth);
        commands.SetViewport(new Viewport(0, 0, _swapchain.Width, _swapchain.Height));
        commands.SetScissor(0, 0, _swapchain.Width, _swapchain.Height);
        commands.SetPipeline(_pipeline);
        commands.SetIndexBuffer(_indices, IndexFormat.UInt16);
        MeshConstants constants = CreateConstants();
        commands.PushConstants(constants);
        commands.DrawIndexed(_indexCount);
        commands.EndRendering();
        commands.Barrier(backBuffer, ResourceState.Present);
        commands.End();
        _device.Submit(commands);
        _swapchain.Present();
    }

    /// <summary>Turns the cube about its vertical axis and looks at it from above and to the side, without perspective.</summary>
    private MeshConstants CreateConstants()
    {
        Matrix4x4 world = Matrix4x4.CreateRotationY(_angle);
        Matrix4x4 view = Matrix4x4.CreateLookAt(Eye, Vector3.Zero, Vector3.UnitY);
        float aspect = (float)_swapchain.Width / _swapchain.Height;
        Matrix4x4 projection = Matrix4x4.CreateOrthographic(ViewHeight * aspect, ViewHeight, 0.1f, 10);
        Matrix4x4 worldViewProjection = world * view * projection;

        Matrix4x4.Invert(world, out Matrix4x4 toObject);
        Vector3 light = Vector3.TransformNormal(Light, toObject);
        return new MeshConstants(worldViewProjection, light, _vertices.BindlessIndex, _texture.BindlessIndex, _sampler.BindlessIndex);
    }

    private ITexture CreateDepth()
    {
        TextureDescription description = new(_swapchain.Width, _swapchain.Height, DepthFormat, TextureUsage.DepthStencil) { Name = "Depth" };
        return _device.CreateTexture(description);
    }

    private IPipeline CreatePipeline(IShaderCompiler compiler)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "Mesh.hlsl");
        string source = File.ReadAllText(path);
        ShaderTarget target = ShaderTarget.Spirv;
        if (_device.Api == GraphicsApi.Direct3D12)
        {
            target = ShaderTarget.Dxil;
        }

        GraphicsPipelineDescription description = new()
        {
            VertexShader = Compile(compiler, source, "VertexMain", ShaderStage.Vertex, target),
            PixelShader = Compile(compiler, source, "PixelMain", ShaderStage.Pixel, target),
            ColorFormats = [_swapchain.Format],
            DepthFormat = DepthFormat,
            Depth = new DepthState(Test: true, Write: true),
            Name = "Mesh",
        };
        return _device.CreateGraphicsPipeline(description);
    }

    private static ShaderBytecode Compile(IShaderCompiler compiler, string source, string entryPoint, ShaderStage stage, ShaderTarget target)
    {
        ShaderSource shader = new(source, entryPoint, stage) { Name = "Mesh.hlsl" };
        ShaderCompileResult result = compiler.Compile(shader, target);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Mesh.hlsl {entryPoint} failed to compile:\n{result.Messages}");
        }

        return new ShaderBytecode(result.Bytecode, entryPoint);
    }
}
