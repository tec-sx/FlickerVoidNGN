using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Renders an empty world with the renderer's sky on, through a camera looking level from a metre up, on a Vulkan device with
/// validation on; skips the test without Vulkan 1.3 and fails it on any logged error.
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
    private string? _paper;
    private Func<LightingMood, LightingMood> _changeMood = static mood => mood;

    public SkyPassTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _game = new Game(_loggerFactory);
        _game.AddRendering();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
    }

    public Camera Camera { get; set; } = new() { Projection = CameraProjection.Perspective };

    public Transform CameraTransform { get; set; } = new() { Position = new Vector3(0, 1, 5) };

    /// <summary>Looks through an orthographic camera tilted down, as the game's views do.</summary>
    public SkyPassTestsBuilder LookingDownOrthographically()
    {
        Camera = new Camera { Projection = CameraProjection.Orthographic };
        Quaternion tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.5f);
        CameraTransform = new Transform { Position = new Vector3(0, 5, 10), Rotation = tilt };
        return this;
    }

    /// <summary>Turns the camera half round about the vertical, to look the other way.</summary>
    public SkyPassTestsBuilder TurnedAround()
    {
        Quaternion turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        CameraTransform = CameraTransform with { Rotation = turn * CameraTransform.Rotation };
        return this;
    }

    /// <summary>Lays a paper of one grey under the scene, as a mood with paper does.</summary>
    public SkyPassTestsBuilder WithPaper(byte grey)
    {
        uint texel = (uint)(grey | grey << 8 | grey << 16 | 255 << 24);
        byte[] data = BitConverter.GetBytes(texel);
        DdsImage image = new(1, 1, PixelFormat.Rgba8UNormSrgb, 1, data);
        string fullPath = Path.Combine(_contentFolder, "Paper.dds");
        using (FileStream stream = File.Create(fullPath))
        {
            image.Write(stream);
        }

        File.WriteAllText(Path.Combine(_contentFolder, "Paper.fvcanvas"), """{ "image": "Paper.tga" }""");
        _paper = "Paper.fvcanvas";
        return this;
    }

    /// <summary>
    /// Paints the scene's sky with a mask of one value in each channel everywhere (the main gradient's position, the band, the
    /// sun's glow held back, the stars), named by a scene environment.
    /// </summary>
    public SkyPassTestsBuilder WithSkyMask(Vector4 channels)
    {
        Half[] texels = new Half[4 * 4 * 4];
        for (int texel = 0; texel < 16; texel++)
        {
            texels[texel * 4] = (Half)channels.X;
            texels[(texel * 4) + 1] = (Half)channels.Y;
            texels[(texel * 4) + 2] = (Half)channels.Z;
            texels[(texel * 4) + 3] = (Half)channels.W;
        }

        byte[] data = MemoryMarshal.AsBytes(texels.AsSpan()).ToArray();
        DdsImage image = new(4, 4, PixelFormat.Rgba16Float, 1, data);
        string fullPath = Path.Combine(_contentFolder, "Sky.dds");
        using (FileStream stream = File.Create(fullPath))
        {
            image.Write(stream);
        }

        _game.World.Create(new SceneEnvironment { SkyMask = "Sky.png" });
        return this;
    }

    /// <summary>Changes the mood the renderer draws with, such as its grade.</summary>
    public SkyPassTestsBuilder WithMood(Func<LightingMood, LightingMood> change)
    {
        _changeMood = change;
        return this;
    }

    /// <summary>The renderer under test, once the first render has built it.</summary>
    public SceneRenderer? Renderer => _renderer;

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

        commands.Begin();
        _renderer.Render(commands, _game.World, color, Camera, CameraTransform);
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
        LightingMood plain = new() { AmbientOcclusion = 0, ContactShadowLength = 0, Tonemapper = Tonemapper.None, Canvas = _paper };
        LightingMood mood = _changeMood(plain);
        return new SceneRenderer(device, content, Format) { Mood = mood, Sky = true };
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
