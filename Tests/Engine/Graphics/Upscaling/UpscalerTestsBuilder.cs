using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Graphics.Tests.Upscaling;

/// <summary>
/// Creates a Vulkan device with validation and DLSS on, skipping the test without Vulkan 1.3 or, unless asked to leave it out,
/// without DLSS (a GPU that has none, or the engine's DLSS library not built beside the tests); upscales flat frames with it and
/// reads the output back. Disposing it fails the test on any error the device logged.
/// </summary>
internal sealed class UpscalerTestsBuilder : IDisposable
{
    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<IDisposable> _created = [];
    private GraphicsFeatures _features = GraphicsFeatures.Dlss;
    private IGraphicsDevice? _device;

    public UpscalerTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
    }

    /// <summary>Creates the device without DLSS, as on a GPU that has none.</summary>
    public UpscalerTestsBuilder WithoutDlss()
    {
        _features = GraphicsFeatures.None;
        return this;
    }

    public IGraphicsDevice Build()
    {
        if (_device is not null)
        {
            return _device;
        }

        GraphicsDeviceOptions options = new() { Validation = true, Features = _features };
        try
        {
            _device = new VulkanGraphicsDevice(options, _loggerFactory);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }

        if (_features != GraphicsFeatures.None && !_device.Features.HasFlag(GraphicsFeatures.Dlss))
        {
            Assert.Skip($"{_device.AdapterName} has no DLSS, or the engine's DLSS library (Source/Native/Dlss) was not built.");
        }

        return _device;
    }

    public IUpscaler CreateUpscaler(UpscaleMode mode, int outputWidth, int outputHeight)
    {
        IGraphicsDevice device = Build();
        UpscalerDescription description = new(mode, outputWidth, outputHeight);
        return Keep(device.CreateUpscaler(description));
    }

    /// <summary>Upscales a frame of one color with nothing drawn in it and nothing moving, and reads the output back.</summary>
    public Vector4[] UpscaleFlat(IUpscaler upscaler, Vector4 color)
    {
        IGraphicsDevice device = Build();
        int width = upscaler.RenderWidth;
        int height = upscaler.RenderHeight;
        Half[] colors = new Half[width * height * 4];
        for (int index = 0; index < colors.Length; index += 4)
        {
            colors[index] = (Half)color.X;
            colors[index + 1] = (Half)color.Y;
            colors[index + 2] = (Half)color.Z;
            colors[index + 3] = (Half)color.W;
        }

        TextureDescription sceneDescription = new(width, height, PixelFormat.Rgba16Float, TextureUsage.ShaderRead) { Name = "Scene" };
        ITexture scene = Keep(device.CreateTexture(sceneDescription, MemoryMarshal.AsBytes(colors.AsSpan())));
        TextureDescription depthDescription = new(width, height, PixelFormat.Depth32Float, TextureUsage.ShaderRead | TextureUsage.DepthStencil) { Name = "Depth" };
        ITexture depth = Keep(device.CreateTexture(depthDescription));
        TextureDescription motionDescription = new(width, height, PixelFormat.Rg16Float, TextureUsage.ShaderRead) { Name = "Motion" };
        byte[] still = new byte[width * height * 4];
        ITexture motion = Keep(device.CreateTexture(motionDescription, still));
        UpscalerDescription upscaled = upscaler.Description;
        TextureUsage outputUsage = TextureUsage.ShaderRead | TextureUsage.ShaderWrite | TextureUsage.CopySource;
        TextureDescription outputDescription = new(upscaled.OutputWidth, upscaled.OutputHeight, PixelFormat.Rgba16Float, outputUsage) { Name = "Upscaled" };
        ITexture output = Keep(device.CreateTexture(outputDescription));

        using ICommandList commands = device.CreateCommandList();
        commands.Begin();
        commands.Barrier(depth, ResourceState.DepthWrite);
        commands.BeginRendering([], new DepthTarget(depth, LoadAction.Clear, 1));
        commands.EndRendering();
        commands.Barrier(depth, ResourceState.ShaderRead);
        commands.Barrier(output, ResourceState.ShaderWrite);
        UpscaleInputs inputs = new(scene, depth, motion, output, Vector2.Zero, 1, true);
        upscaler.Upscale(commands, inputs);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();
        return ReadColors(device, output);
    }

    public void Dispose()
    {
        for (int index = _created.Count - 1; index >= 0; index--)
        {
            _created[index].Dispose();
        }

        _device?.Dispose();
        _loggerFactory.Dispose();
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    /// <summary>Copies an Rgba16Float texture to the CPU.</summary>
    private static Vector4[] ReadColors(IGraphicsDevice device, ITexture texture)
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

        Vector4[] colors = new Vector4[description.Width * description.Height];
        byte[] row = new byte[description.Width * 8];
        for (int y = 0; y < description.Height; y++)
        {
            readback.Read(row, (long)y * pitch);
            ReadOnlySpan<Half> halves = MemoryMarshal.Cast<byte, Half>(row);
            for (int x = 0; x < description.Width; x++)
            {
                Vector4 pixel = new((float)halves[x * 4], (float)halves[x * 4 + 1], (float)halves[x * 4 + 2], (float)halves[x * 4 + 3]);
                colors[y * description.Width + x] = pixel;
            }
        }

        return colors;
    }

    private T Keep<T>(T resource)
        where T : IDisposable
    {
        _created.Add(resource);
        return resource;
    }
}
