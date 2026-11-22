using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Tests;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Cameras.Tests.Transitions;

/// <summary>
/// Builds a game with rendering and cameras, a red quad at the origin in a temporary content folder, and a scene renderer set up
/// as the host sets one up (so it has the transition pass) on a Vulkan device with validation on, skipping the test without
/// Vulkan 1.3. Renders 4x4 frames from an active camera at the given place, with the given transition, under full ambient light.
/// </summary>
internal sealed class TransitionPassTestsBuilder : IDisposable
{
    private const int Size = 4;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _contentFolder;
    private readonly Game _game;
    private IGraphicsDevice? _device;
    private SceneRenderer? _renderer;

    public TransitionPassTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _game = new Game(_loggerFactory);
        _game.AddRendering();
        _game.AddCameras();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        TestModelBuilder model = new TestModelBuilder().WithQuad(0.5f).WithBaseColor(new Vector4(1, 0, 0, 1));
        ModelData data = model.Build();
        string modelPath = Path.Combine(_contentFolder, "Quad.fvmodel");
        using (FileStream stream = File.Create(modelPath))
        {
            ModelFile.Write(data, stream);
        }

        Spawn("""{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""");
    }

    /// <summary>The camera at the origin in front of the quad, which fills its middle four pixels.</summary>
    public static Transform OnTheQuad => new() { Position = new Vector3(0, 0, 5) };

    /// <summary>A camera looking at nothing.</summary>
    public static Transform AwayFromTheQuad => new() { Position = new Vector3(100, 0, 5) };

    public static Camera Lens => new() { OrthographicHeight = 2 };

    /// <summary>Renders the world through an active camera with the transition and reads it back as Rgba8 pixels, top row first.</summary>
    public uint[] Render(ActiveCamera active)
    {
        Camera lens = Lens;
        Transform place = active.Pose;
        _game.World.Create(lens, place, active);
        IGraphicsDevice device = BuildDevice();
        ContentManager content = new(_contentFolder);
        _renderer ??= CreateRenderer(device, content);

        TextureDescription colorDescription = new(Size, Size, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture color = device.CreateTexture(colorDescription);
        int pitch = PixelFormat.Rgba8UNorm.GetCopyRowPitch(Size);
        BufferDescription readbackDescription = new(pitch * Size, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        _renderer.Render(commands, _game.World, color);
        commands.Barrier(color, ResourceState.CopySource);
        commands.CopyTextureToBuffer(color, readback, 0);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] pixels = new uint[Size * Size];
        byte[] row = new byte[Size * 4];
        for (int y = 0; y < Size; y++)
        {
            readback.Read(row, (long)y * pitch);
            Span<uint> destination = pixels.AsSpan(y * Size, Size);
            MemoryMarshal.Cast<byte, uint>(row).CopyTo(destination);
        }

        return pixels;
    }

    public static (byte Red, byte Green, byte Blue, byte Alpha) Channels(uint pixel) => ((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16), (byte)(pixel >> 24));

    public void Dispose()
    {
        _renderer?.Dispose();
        _device?.Dispose();
        _game.Dispose();
        _loggerFactory.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    private SceneRenderer CreateRenderer(IGraphicsDevice device, ContentManager content)
    {
        SceneRenderer renderer = new(device, content, PixelFormat.Rgba8UNorm)
        {
            Mood = new LightingMood { Ambient = 1, AmbientOcclusion = 0, ContactShadowLength = 0, Tonemapper = Tonemapper.None },
        };
        SceneRendererSetup setup = _game.Services.GetService<SceneRendererSetup>()!;
        setup.Apply(renderer);
        return renderer;
    }

    private void Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        _game.Spawner.Spawn(scene, _game.World);
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
