using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Graphics.Vulkan;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Shaders;

using Microsoft.Extensions.Logging;

using SharpGLTF.Schema2;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds a game with rendering, its models in a temporary content folder, and the scene renderer on a Vulkan device with
/// validation on, skipping the test without Vulkan 1.3. Renders 4x4 frames unless told otherwise; disposing it fails the test on any logged error.
/// </summary>
internal sealed class SceneRendererTestsBuilder : IDisposable
{

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly DxcShaderCompiler _compiler = new();
    private readonly string _contentFolder;
    private readonly Game _game;
    private IGraphicsDevice? _device;
    private SceneRenderer? _renderer;
    private Vector3 _sunDirection = -Vector3.UnitZ;
    private float _ambient = 1;
    private float _fogDensity;
    private Vector3 _fogColor = Vector3.One;
    private float _fogHeight;
    private float _fogHeightFalloff;
    private int _targetSize = 4;
    private LightingMood _mood = new() { AmbientOcclusion = 0, ContactShadowLength = 0 };
    private Action<SceneRenderer>? _configure;

    public SceneRendererTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _game = new Game(_loggerFactory);
        _game.AddRendering();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
    }

    /// <summary>Writes a .glb built by the importer tests' builder into the content folder.</summary>
    public SceneRendererTestsBuilder WithModel(string path, Action<GltfImporterTestsBuilder> build)
    {
        GltfImporterTestsBuilder model = new();
        build(model);
        ModelRoot root = model.BuildModel();
        string fullPath = Path.Combine(_contentFolder, path);
        root.SaveGLB(fullPath);
        return this;
    }

    /// <summary>Writes an uncompressed TGA from Rgba8 pixels, top row first, into the content folder.</summary>
    public SceneRendererTestsBuilder WithTexture(string path, int width, int height, uint[] pixels)
    {
        byte[] file = new byte[18 + (pixels.Length * 4)];
        file[2] = 2;
        file[12] = (byte)width;
        file[13] = (byte)(width >> 8);
        file[14] = (byte)height;
        file[15] = (byte)(height >> 8);
        file[16] = 32;
        file[17] = 0x28;
        for (int index = 0; index < pixels.Length; index++)
        {
            (byte red, byte green, byte blue) = Channels(pixels[index]);
            int offset = 18 + (index * 4);
            file[offset] = blue;
            file[offset + 1] = green;
            file[offset + 2] = red;
            file[offset + 3] = (byte)(pixels[index] >> 24);
        }

        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllBytes(fullPath, file);
        return this;
    }

    /// <summary>Saves the probes into the content folder.</summary>
    public SceneRendererTestsBuilder WithProbes(string path, IrradianceProbes probes)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        using FileStream stream = File.Create(fullPath);
        probes.Save(stream);
        return this;
    }

    public SceneRendererTestsBuilder WithScene(params string[] entities)
    {
        string json = $$"""{ "entities": [{{string.Join(",", entities)}}] }""";
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        _game.Spawner.Spawn(scene!, _game.World);
        return this;
    }

    /// <summary>Without a call, the ambient light is full, so meshes show their plain base color.</summary>
    public SceneRendererTestsBuilder WithLight(Vector3 sunDirection, float ambient)
    {
        _sunDirection = sunDirection;
        _ambient = ambient;
        return this;
    }

    /// <summary>Changes the mood the renderer and baker get; the sun, ambient light and fog come from their own calls.</summary>
    public SceneRendererTestsBuilder WithMood(Func<LightingMood, LightingMood> change)
    {
        _mood = change(_mood);
        return this;
    }

    /// <summary>Without a height falloff, the fog is even everywhere.</summary>
    public SceneRendererTestsBuilder WithFog(float density, Vector3 color, float height = 0, float heightFalloff = 0)
    {
        _fogDensity = density;
        _fogColor = color;
        _fogHeight = height;
        _fogHeightFalloff = heightFalloff;
        return this;
    }

    /// <summary>Sets up the renderer itself, such as its feature switches, before it draws.</summary>
    public SceneRendererTestsBuilder WithRenderer(Action<SceneRenderer> configure)
    {
        _configure = configure;
        return this;
    }

    public SceneRendererTestsBuilder WithTargetSize(int size)
    {
        _targetSize = size;
        return this;
    }

    /// <summary>Renders the world into a new 4x4 target (or the size given) and reads it back as Rgba8 pixels, top row first.</summary>
    public uint[] Render()
    {
        IGraphicsDevice device = BuildDevice();
        _renderer ??= new SceneRenderer(device, _compiler, new ContentManager(_contentFolder), PixelFormat.Rgba8UNorm);
        _renderer.Mood = BuildMood();
        _configure?.Invoke(_renderer);

        TextureDescription colorDescription = new(_targetSize, _targetSize, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture color = device.CreateTexture(colorDescription);
        int pitch = PixelFormat.Rgba8UNorm.GetCopyRowPitch(_targetSize);
        BufferDescription readbackDescription = new(pitch * _targetSize, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        _renderer.Render(commands, _game.World, color);
        commands.Barrier(color, ResourceState.CopySource);
        commands.CopyTextureToBuffer(color, readback, 0);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] pixels = new uint[_targetSize * _targetSize];
        byte[] row = new byte[_targetSize * 4];
        for (int y = 0; y < _targetSize; y++)
        {
            readback.Read(row, (long)y * pitch);
            Span<uint> destination = pixels.AsSpan(y * _targetSize, _targetSize);
            MemoryMarshal.Cast<byte, uint>(row).CopyTo(destination);
        }

        return pixels;
    }

    /// <summary>Creates a baker on the test device, reading the same content folder, lit like the renderer.</summary>
    public IrradianceBaker BuildBaker()
    {
        IGraphicsDevice device = BuildDevice();
        IrradianceBaker baker = new(device, _compiler, new ContentManager(_contentFolder));
        baker.Renderer.Mood = BuildMood();
        return baker;
    }

    public World World => _game.World;

    /// <summary>A pixel as Rgba8UNorm bytes read back little-endian.</summary>
    public static uint Rgba(byte r, byte g, byte b, byte a) => (uint)(r | g << 8 | b << 16 | a << 24);

    public static (byte Red, byte Green, byte Blue) Channels(uint pixel) => ((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16));

    public void Dispose()
    {
        _renderer?.Dispose();
        _device?.Dispose();
        _game.Dispose();
        _compiler.Dispose();
        _loggerFactory.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    // Screen-space shadows and post effects stay off unless a test asks for them, so pixels show the plain lighting.
    private LightingMood BuildMood()
    {
        LightingMood mood = _mood with
        {
            SunDirection = _sunDirection,
            Ambient = _ambient,
            FogDensity = _fogDensity,
            FogColor = _fogColor,
            FogHeight = _fogHeight,
            FogHeightFalloff = _fogHeightFalloff,
        };
        return mood;
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
