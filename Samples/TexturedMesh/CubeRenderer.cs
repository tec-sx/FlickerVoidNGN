using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>Records a lit, textured cube seen through an orthographic camera into a color target and a depth target.</summary>
public sealed class CubeRenderer : IDisposable
{
    public const PixelFormat DepthFormat = PixelFormat.Depth32Float;

    private const float ViewHeight = 2.6f;

    private static readonly Vector4 Background = new(0.11f, 0.10f, 0.13f, 1);
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));
    private static readonly Vector3 Eye = new(3, 2.4f, 3);

    private readonly IGraphicsDevice _device;
    private readonly IPipeline _pipeline;
    private readonly IBuffer _vertices;
    private readonly IBuffer _indices;
    private readonly int _indexCount;
    private readonly ITexture _texture;
    private readonly ISampler _sampler;

    public CubeRenderer(IGraphicsDevice device, IShaderCompiler compiler, PixelFormat colorFormat)
    {
        _device = device;
        _pipeline = CreatePipeline(compiler, colorFormat);

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
    }

    /// <summary>Clears both targets and draws the cube turned by the angle about its vertical axis; leaves the color target in the RenderTarget state.</summary>
    public void Record(ICommandList commands, ITexture colorTarget, ITexture depthTarget, float angle)
    {
        int width = colorTarget.Description.Width;
        int height = colorTarget.Description.Height;
        commands.Barrier(colorTarget, ResourceState.RenderTarget);
        commands.Barrier(depthTarget, ResourceState.DepthWrite);
        ColorTarget color = new(colorTarget, LoadAction.Clear, Background);
        DepthTarget depth = new(depthTarget, LoadAction.Clear);
        commands.BeginRendering([color], depth);
        commands.SetViewport(new Viewport(0, 0, width, height));
        commands.SetScissor(0, 0, width, height);
        commands.SetPipeline(_pipeline);
        commands.SetIndexBuffer(_indices, IndexFormat.UInt16);
        float aspect = (float)width / height;
        MeshConstants constants = CreateConstants(angle, aspect);
        commands.PushConstants(constants);
        commands.DrawIndexed(_indexCount);
        commands.EndRendering();
    }

    public ITexture CreateDepth(int width, int height)
    {
        TextureDescription description = new(width, height, DepthFormat, TextureUsage.DepthStencil) { Name = "Depth" };
        return _device.CreateTexture(description);
    }

    public void Dispose()
    {
        _sampler.Dispose();
        _texture.Dispose();
        _indices.Dispose();
        _vertices.Dispose();
        _pipeline.Dispose();
    }

    /// <summary>Turns the cube about its vertical axis and looks at it from above and to the side, without perspective.</summary>
    private MeshConstants CreateConstants(float angle, float aspect)
    {
        Matrix4x4 world = Matrix4x4.CreateRotationY(angle);
        Matrix4x4 view = Matrix4x4.CreateLookAt(Eye, Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreateOrthographic(ViewHeight * aspect, ViewHeight, 0.1f, 10);
        Matrix4x4 worldViewProjection = world * view * projection;

        Matrix4x4.Invert(world, out Matrix4x4 toObject);
        Vector3 light = Vector3.TransformNormal(Light, toObject);
        return new MeshConstants(worldViewProjection, light, _vertices.BindlessIndex, _texture.BindlessIndex, _sampler.BindlessIndex);
    }

    private IPipeline CreatePipeline(IShaderCompiler compiler, PixelFormat colorFormat)
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
            ColorFormats = [colorFormat],
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
