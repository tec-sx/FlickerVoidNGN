using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;


namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds a game with rendering, its models in a temporary content folder, and the scene renderer on a Vulkan device with
/// validation on, skipping the test without Vulkan 1.3. Renders 4x4 frames unless told otherwise; disposing it fails the test on any logged error.
/// </summary>
internal sealed class SceneRendererTestsBuilder : IDisposable
{

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _contentFolder;
    private readonly ContentManager _content;
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
    private LightingMood _mood = new() { AmbientOcclusion = 0, ContactShadowLength = 0, Tonemapper = Tonemapper.None };
    private Action<SceneRenderer>? _configure;

    public SceneRendererTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _game = new Game(_loggerFactory);
        _game.AddRendering();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        _content = new ContentManager(_contentFolder);
    }

    /// <summary>Writes a model as the content build leaves one named <paramref name="path"/>: baked, with the .fvmodel extension.</summary>
    public SceneRendererTestsBuilder WithModel(string path, Action<TestModelBuilder> build)
    {
        TestModelBuilder model = new();
        build(model);
        ModelData data = model.Build();
        string builtPath = Path.ChangeExtension(path, ".fvmodel");
        string fullPath = Path.Combine(_contentFolder, builtPath);
        using FileStream stream = File.Create(fullPath);
        ModelFile.Write(data, stream);
        return this;
    }

    /// <summary>Reports a content file changed, as the content watcher would.</summary>
    public void NotifyChanged(string path)
    {
        _content.NotifyChanged(path);
    }

    /// <summary>
    /// Writes Rgba8 pixels, top row first, as the content build leaves a texture named <paramref name="path"/>: an sRGB DDS
    /// with the .dds extension, uncompressed so the pixels stay exact.
    /// </summary>
    public SceneRendererTestsBuilder WithTexture(string path, int width, int height, uint[] pixels, PixelFormat format = PixelFormat.Rgba8UNormSrgb, bool mips = false)
    {
        List<byte> data = [.. MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray()];
        int levels = 1;
        uint[] level = pixels;
        int levelWidth = width;
        int levelHeight = height;
        while (mips && (levelWidth > 1 || levelHeight > 1))
        {
            level = HalveLevel(level, levelWidth, levelHeight);
            levelWidth = Math.Max(levelWidth / 2, 1);
            levelHeight = Math.Max(levelHeight / 2, 1);
            data.AddRange(MemoryMarshal.AsBytes(level.AsSpan()).ToArray());
            levels++;
        }

        DdsImage image = new(width, height, format, levels, [.. data]);
        string builtPath = Path.ChangeExtension(path, ".dds");
        string fullPath = Path.Combine(_contentFolder, builtPath);
        using FileStream stream = File.Create(fullPath);
        image.Write(stream);
        return this;
    }

    /// <summary>Writes a text file, such as a material, into the content folder.</summary>
    public SceneRendererTestsBuilder WithFile(string path, string text)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, text);
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

    /// <summary>Adds a component the scene files can't give, such as a tag systems set, to the scene's entity of that name.</summary>
    public SceneRendererTestsBuilder WithComponent<T>(string name, T component)
    {
        Entity? found = null;
        QueryDescription named = new QueryDescription().WithAll<EntityName>();
        _game.World.Query(in named, (Entity entity, ref EntityName entityName) =>
        {
            if (entityName.Value == name)
            {
                found = entity;
            }
        });
        _game.World.Add(found!.Value, component);
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
        _renderer ??= new SceneRenderer(device, _content, PixelFormat.Rgba8UNorm);
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
        IrradianceBaker baker = new(device, new ContentManager(_contentFolder));
        baker.Renderer.Mood = BuildMood();
        return baker;
    }

    public World World => _game.World;

    /// <summary>What the last <see cref="Render"/> drew and culled.</summary>
    public RenderStatistics Statistics => _renderer!.Statistics;

    /// <summary>A pixel as Rgba8UNorm bytes read back little-endian.</summary>
    /// <summary>The next mip level of RGBA8 texels: each texel the average of the two by two below it.</summary>
    private static uint[] HalveLevel(uint[] texels, int width, int height)
    {
        int halfWidth = Math.Max(width / 2, 1);
        int halfHeight = Math.Max(height / 2, 1);
        uint[] half = new uint[halfWidth * halfHeight];
        for (int y = 0; y < halfHeight; y++)
        {
            for (int x = 0; x < halfWidth; x++)
            {
                uint packed = 0;
                for (int channel = 0; channel < 4; channel++)
                {
                    int sum = 0;
                    int count = 0;
                    for (int dy = 0; dy < 2 && y * 2 + dy < height; dy++)
                    {
                        for (int dx = 0; dx < 2 && x * 2 + dx < width; dx++)
                        {
                            uint texel = texels[(y * 2 + dy) * width + x * 2 + dx];
                            sum += (int)((texel >> (channel * 8)) & 0xFF);
                            count++;
                        }
                    }

                    uint average = (uint)(sum / count);
                    packed |= average << (channel * 8);
                }

                half[y * halfWidth + x] = packed;
            }
        }

        return half;
    }

    public static uint Rgba(byte r, byte g, byte b, byte a) => (uint)(r | g << 8 | b << 16 | a << 24);

    public static (byte Red, byte Green, byte Blue) Channels(uint pixel) => ((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16));

    public void Dispose()
    {
        _renderer?.Dispose();
        _device?.Dispose();
        _game.Dispose();
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
