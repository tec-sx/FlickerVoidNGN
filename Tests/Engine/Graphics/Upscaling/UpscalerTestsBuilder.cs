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
/// without DLSS (a GPU that has none, or the engine's DLSS library not built beside the tests), or without Ray Reconstruction
/// when asked for it; upscales flat frames with it and reads the output back. Disposing it fails the test on any error the
/// device logged.
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

    /// <summary>Creates the device with Ray Reconstruction too, skipping the test where it has none.</summary>
    public UpscalerTestsBuilder WithRayReconstruction()
    {
        _features = GraphicsFeatures.Dlss | GraphicsFeatures.RayReconstruction;
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

        if (_features.HasFlag(GraphicsFeatures.RayReconstruction) && !_device.Features.HasFlag(GraphicsFeatures.RayReconstruction))
        {
            Assert.Skip($"{_device.AdapterName} has no DLSS Ray Reconstruction, or its runtime is not beside the tests.");
        }

        return _device;
    }

    /// <summary>Disposes the device and what was made on it, and creates another the same way, without skipping where it has no DLSS.</summary>
    public IGraphicsDevice Remake()
    {
        Build();
        ReleaseDevice();
        GraphicsDeviceOptions options = new() { Validation = true, Features = _features };
        _device = new VulkanGraphicsDevice(options, _loggerFactory);
        return _device;
    }

    public IUpscaler CreateUpscaler(UpscaleMode mode, int outputWidth, int outputHeight)
    {
        UpscalerDescription description = new(mode, outputWidth, outputHeight);
        return CreateUpscaler(description);
    }

    public IUpscaler CreateUpscaler(UpscalerDescription description)
    {
        IGraphicsDevice device = Build();
        return Keep(device.CreateUpscaler(description));
    }

    /// <summary>
    /// Upscales a frame of one color with nothing drawn in it and nothing moving, and reads the output back; a denoising upscaler is
    /// told the frame shows a white floor facing the camera, as rough as can be.
    /// </summary>
    public Vector4[] UpscaleFlat(IUpscaler upscaler, Vector4 color)
    {
        IGraphicsDevice device = Build();
        int width = upscaler.RenderWidth;
        int height = upscaler.RenderHeight;
        ITexture scene = CreateFlat(device, width, height, color, "Scene");
        TextureDescription depthDescription = new(width, height, PixelFormat.Depth32Float, TextureUsage.ShaderRead | TextureUsage.DepthStencil) { Name = "Depth" };
        ITexture depth = Keep(device.CreateTexture(depthDescription));
        TextureDescription motionDescription = new(width, height, PixelFormat.Rg16Float, TextureUsage.ShaderRead) { Name = "Motion" };
        byte[] still = new byte[width * height * 4];
        ITexture motion = Keep(device.CreateTexture(motionDescription, still));
        UpscalerDescription upscaled = upscaler.Description;
        TextureUsage outputUsage = TextureUsage.ShaderRead | TextureUsage.ShaderWrite | TextureUsage.CopySource | TextureUsage.CopyDestination;
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
        if (upscaled.Denoises)
        {
            ITexture diffuse = CreateFlat(device, width, height, Vector4.One, "Diffuse albedo");
            ITexture specular = CreateFlat(device, width, height, Vector4.Zero, "Specular albedo");
            ITexture normals = CreateFlat(device, width, height, new Vector4(0, 0, 1, 1), "Normals");
            Matrix4x4 projection = Matrix4x4.CreateOrthographic(width, height, 0.1f, 10);
            inputs = inputs with { Guides = new DenoiseGuides(diffuse, specular, normals, Matrix4x4.Identity, projection) };
        }

        upscaler.Upscale(commands, inputs);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();
        return ReadColors(device, output);
    }

    public void Dispose()
    {
        ReleaseDevice();
        _loggerFactory.Dispose();
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    /// <summary>An Rgba16Float texture of one color the shaders read.</summary>
    private ITexture CreateFlat(IGraphicsDevice device, int width, int height, Vector4 color, string name)
    {
        Half[] texels = new Half[width * height * 4];
        for (int index = 0; index < texels.Length; index += 4)
        {
            texels[index] = (Half)color.X;
            texels[index + 1] = (Half)color.Y;
            texels[index + 2] = (Half)color.Z;
            texels[index + 3] = (Half)color.W;
        }

        TextureDescription description = new(width, height, PixelFormat.Rgba16Float, TextureUsage.ShaderRead) { Name = name };
        return Keep(device.CreateTexture(description, MemoryMarshal.AsBytes(texels.AsSpan())));
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

    private void ReleaseDevice()
    {
        for (int index = _created.Count - 1; index >= 0; index--)
        {
            _created[index].Dispose();
        }

        _created.Clear();
        _device?.Dispose();
        _device = null;
    }
}
