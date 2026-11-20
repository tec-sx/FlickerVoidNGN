using System.Runtime.InteropServices;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Editor.Core.Tests.Viewports;

/// <summary>
/// Renders an empty world with the sky pass after the scene renderer's own passes, through an editor camera, on a Vulkan device
/// with validation on; skips the test without Vulkan 1.3 and fails it on any logged error.
/// </summary>
internal sealed class SkyPassTestsBuilder : IDisposable
{
    public const int TargetSize = 63;

    private const PixelFormat Format = PixelFormat.Rgba8UNormSrgb;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _contentFolder;
    private readonly Game _game;
    private IGraphicsDevice? _device;
    private SceneRenderer? _renderer;

    public SkyPassTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _game = new Game(_loggerFactory);
        _game.AddRendering();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
    }

    public EditorCamera Camera { get; } = new();

    /// <summary>The pass under test, once the first render has built it.</summary>
    public SkyPass? Sky { get; private set; }

    /// <summary>Renders into a new target and reads it back as Rgba8 pixels, top row first.</summary>
    public uint[] Render()
    {
        IGraphicsDevice device = BuildDevice();
        _renderer ??= BuildRenderer(device);

        TextureDescription colorDescription = new(TargetSize, TargetSize, Format, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture color = device.CreateTexture(colorDescription);
        int pitch = Format.GetCopyRowPitch(TargetSize);
        BufferDescription readbackDescription = new(pitch * TargetSize, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        Camera camera = Camera.ToCamera();
        Transform transform = Camera.ToTransform();
        commands.Begin();
        _renderer.Render(commands, _game.World, color, camera, transform);
        commands.Barrier(color, ResourceState.CopySource);
        commands.CopyTextureToBuffer(color, readback, 0);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] pixels = new uint[TargetSize * TargetSize];
        byte[] row = new byte[TargetSize * 4];
        for (int y = 0; y < TargetSize; y++)
        {
            readback.Read(row, (long)y * pitch);
            Span<uint> destination = pixels.AsSpan(y * TargetSize, TargetSize);
            MemoryMarshal.Cast<byte, uint>(row).CopyTo(destination);
        }

        return pixels;
    }

    public static (byte Red, byte Green, byte Blue) Channels(uint pixel) => ((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16));

    /// <summary>The sum of a pixel's channels.</summary>
    public static int Brightness(uint pixel)
    {
        (byte red, byte green, byte blue) = Channels(pixel);
        return red + green + blue;
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _device?.Dispose();
        _game.Dispose();
        _loggerFactory.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    private SceneRenderer BuildRenderer(IGraphicsDevice device)
    {
        ContentManager content = new(_contentFolder);
        LightingMood mood = new() { AmbientOcclusion = 0, ContactShadowLength = 0, Tonemapper = Tonemapper.None };
        SceneRenderer renderer = new(device, content, Format) { Mood = mood };
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Editor");
        Sky = new SkyPass(device, shaders, Format);
        renderer.Pipeline.Add(Sky);
        return renderer;
    }

    private IGraphicsDevice BuildDevice()
    {
        if (_device is not null)
        {
            return _device;
        }

        GraphicsDeviceOptions options = new() { Validation = true };
        try
        {
            _device = new VulkanGraphicsDevice(options, _loggerFactory);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }

        return _device;
    }
}
