using System.Numerics;
using System.Runtime.InteropServices;
using FlickerVoid.Engine.Graphics.Direct3D12;
using FlickerVoid.Engine.Graphics.Vulkan;
using FlickerVoid.Engine.Shaders;
using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>
/// Creates the device under test with validation on, skipping the test when the machine can't run the API, and
/// compiles shaders and reads textures back for it. Disposing it fails the test on any error the device logged.
/// </summary>
internal sealed class GraphicsDeviceTestsBuilder : IDisposable
{
    public const int TargetSize = 4;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly DxcShaderCompiler _compiler = new();
    private readonly GraphicsApi _api;

    public GraphicsDeviceTestsBuilder(GraphicsApi api)
    {
        _api = api;
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
    }

    public static TheoryData<GraphicsApi> Apis => [GraphicsApi.Direct3D12, GraphicsApi.Vulkan];

    public IGraphicsDevice Build()
    {
        GraphicsDeviceOptions options = new() { Validation = true };
        try
        {
            switch (_api)
            {
                case GraphicsApi.Direct3D12:
                    return new D3D12GraphicsDevice(options, _loggerFactory);
                case GraphicsApi.Vulkan:
                    return new VulkanGraphicsDevice(options, _loggerFactory);
                default:
                    throw new ArgumentOutOfRangeException(nameof(_api), _api, null);
            }
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }
    }

    public ShaderBytecode Compile(string source, ShaderStage stage)
    {
        ShaderTarget target = ShaderTarget.Spirv;
        if (_api == GraphicsApi.Direct3D12)
        {
            target = ShaderTarget.Dxil;
        }

        ShaderSource shader = new(source, "main", stage);
        ShaderCompileResult result = _compiler.Compile(shader, target);
        result.Succeeded.Should().BeTrue(result.Messages);
        return new ShaderBytecode(result.Bytecode);
    }

    public static ITexture CreateTarget(IGraphicsDevice device)
    {
        TextureDescription description = new(TargetSize, TargetSize, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource);
        return device.CreateTexture(description);
    }

    public IPipeline CreatePipeline(IGraphicsDevice device, string vertexSource, string pixelSource)
    {
        ShaderBytecode vertex = Compile(vertexSource, ShaderStage.Vertex);
        ShaderBytecode pixel = Compile(pixelSource, ShaderStage.Pixel);
        GraphicsPipelineDescription description = new()
        {
            VertexShader = vertex,
            PixelShader = pixel,
            ColorFormats = [PixelFormat.Rgba8UNorm],
            CullMode = CullMode.None,
        };
        return device.CreateGraphicsPipeline(description);
    }

    /// <summary>Starts rendering to the target, cleared to the color, with the viewport and scissor covering it.</summary>
    public static void BeginRendering(ICommandList commands, ITexture target, Vector4 clearColor)
    {
        commands.Barrier(target, ResourceState.RenderTarget);
        ColorTarget color = new(target, LoadAction.Clear, clearColor);
        commands.BeginRendering([color]);
        commands.SetViewport(new Viewport(0, 0, TargetSize, TargetSize));
        commands.SetScissor(0, 0, TargetSize, TargetSize);
    }

    /// <summary>Copies the texture to the CPU as tightly packed rows of 4-byte pixels.</summary>
    public static uint[] ReadPixels(IGraphicsDevice device, ITexture texture)
    {
        TextureDescription description = texture.Description;
        int pitch = description.Format.GetCopyRowPitch(description.Width);
        BufferDescription readbackDescription = new((long)pitch * description.Height, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(texture, ResourceState.CopySource);
        commands.CopyTextureToBuffer(texture, readback, 0);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] pixels = new uint[description.Width * description.Height];
        byte[] row = new byte[description.Width * 4];
        for (int y = 0; y < description.Height; y++)
        {
            readback.Read(row, (long)y * pitch);
            Span<uint> destination = pixels.AsSpan(y * description.Width, description.Width);
            MemoryMarshal.Cast<byte, uint>(row).CopyTo(destination);
        }

        return pixels;
    }

    /// <summary>A pixel as Rgba8UNorm bytes read back little-endian.</summary>
    public static uint Rgba(byte r, byte g, byte b, byte a) => (uint)(r | g << 8 | b << 16 | a << 24);

    public void Dispose()
    {
        _compiler.Dispose();
        _loggerFactory.Dispose();
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }
}
