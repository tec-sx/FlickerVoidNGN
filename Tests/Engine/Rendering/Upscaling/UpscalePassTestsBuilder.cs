using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Rendering.Tests.Upscaling;

/// <summary>
/// Renders a scene through its first camera into a square output on a Vulkan device with validation on, plainly lit, with the
/// device's DLSS, and its Ray Reconstruction, stood in for when asked; skips the test without Vulkan 1.3, or without ray queries
/// when it needs them, and fails it on any logged error.
/// </summary>
internal sealed class UpscalePassTestsBuilder : IDisposable
{
    public const int OutputSize = 48;

    private const PixelFormat Format = PixelFormat.Rgba8UNorm;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _contentFolder;
    private readonly Game _game;
    private IGraphicsDevice? _device;
    private UpscalingDevice? _upscalingDevice;
    private SceneRenderer? _renderer;
    private GraphicsFeatures _features = GraphicsFeatures.None;
    private GraphicsFeatures _upscaling = GraphicsFeatures.None;
    private string? _paper;
    private uint _cuts;
    private Action<SceneRenderer> _configure = static _ => { };

    public UpscalePassTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _game = new Game(_loggerFactory);
        _game.AddRendering();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
    }

    /// <summary>The upscalers the device made, in order; empty without <see cref="WithDlss"/>.</summary>
    public IReadOnlyList<StandInUpscaler> Upscalers => _upscalingDevice?.Upscalers ?? [];

    public UpscalePassTestsBuilder WithScene(params string[] entities)
    {
        string json = $$"""{ "entities": [{{string.Join(",", entities)}}] }""";
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        _game.Spawner.Spawn(scene!, _game.World);
        return this;
    }

    /// <summary>Gives the device DLSS, stood in for by <see cref="StandInUpscaler"/>s.</summary>
    public UpscalePassTestsBuilder WithDlss()
    {
        _upscaling = GraphicsFeatures.Dlss;
        return this;
    }

    /// <summary>Gives the device DLSS with Ray Reconstruction, stood in for, and ray queries, skipping the test without them.</summary>
    public UpscalePassTestsBuilder WithRayReconstruction()
    {
        _upscaling = GraphicsFeatures.Dlss | GraphicsFeatures.RayReconstruction;
        return WithRayQueries();
    }

    /// <summary>Creates the device with ray queries, skipping the test where it has none.</summary>
    public UpscalePassTestsBuilder WithRayQueries()
    {
        _features = GraphicsFeatures.RayQueries;
        return this;
    }

    /// <summary>Writes a text file, such as a material, into the content folder.</summary>
    public UpscalePassTestsBuilder WithFile(string path, string text)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, text);
        return this;
    }

    public UpscalePassTestsBuilder WithUpscaling(UpscaleMode mode)
    {
        return WithRenderer(renderer => renderer.Upscaling = mode);
    }

    /// <summary>Sets the renderer up further before every frame.</summary>
    public UpscalePassTestsBuilder WithRenderer(Action<SceneRenderer> configure)
    {
        Action<SceneRenderer> before = _configure;
        _configure = renderer =>
        {
            before(renderer);
            configure(renderer);
        };
        return this;
    }

    /// <summary>Lays a paper of one grey under the scene, as a mood with paper does.</summary>
    public UpscalePassTestsBuilder WithPaper(byte grey)
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

    /// <summary>Cuts the scene's first camera to its own view, as a director cutting to a shot does.</summary>
    public UpscalePassTestsBuilder WithCut()
    {
        _cuts++;
        QueryDescription cameras = new QueryDescription().WithAll<Camera, Transform>();
        Entity camera = Entity.Null;
        _game.World.Query(in cameras, (Entity entity) =>
        {
            if (camera == Entity.Null)
            {
                camera = entity;
            }
        });
        Camera lens = _game.World.Get<Camera>(camera);
        Transform pose = _game.World.Get<Transform>(camera);
        ActiveCamera active = new() { Lens = lens, Pose = pose, Cuts = _cuts };
        if (_game.World.Has<ActiveCamera>(camera))
        {
            _game.World.Set(camera, active);
        }
        else
        {
            _game.World.Add(camera, active);
        }

        return this;
    }

    /// <summary>Renders a frame into a new output of that size and reads it back as Rgba8 pixels, top row first.</summary>
    public uint[] Render(int size = OutputSize)
    {
        return Render(size, static (_, _) => { });
    }

    /// <summary>Renders that many frames in a row, as a window would, and gives the last one's pixels.</summary>
    public uint[] RenderFrames(int count)
    {
        uint[] pixels = Render();
        for (int frame = 1; frame < count; frame++)
        {
            pixels = Render();
        }

        return pixels;
    }

    /// <summary>Renders that many frames in a row and reads back the last one's motion, at the size it was drawn at, top row first.</summary>
    public Vector2[] RenderMotion(int frames)
    {
        for (int frame = 1; frame < frames; frame++)
        {
            Render();
        }

        IGraphicsDevice device = BuildDevice();
        int widestPitch = PixelFormat.Rgba16Float.GetCopyRowPitch(OutputSize);
        BufferDescription readbackDescription = new(widestPitch * OutputSize, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        GBufferReadbackPass pass = new(static gBuffer => gBuffer.Motion, readback);
        Render(OutputSize, (renderer, _) => renderer.Pipeline.Add(pass));
        _renderer!.Pipeline.Remove(pass);

        ITexture motion = pass.Texture!;
        int width = motion.Description.Width;
        int height = motion.Description.Height;
        int rowSize = pass.Format.GetRowSize(width);
        int pitch = pass.Format.GetCopyRowPitch(width);
        Vector2[] pairs = new Vector2[width * height];
        byte[] row = new byte[rowSize];
        for (int y = 0; y < height; y++)
        {
            readback.Read(row, (long)y * pitch);
            ReadOnlySpan<Half> halfs = MemoryMarshal.Cast<byte, Half>(row);
            for (int x = 0; x < width; x++)
            {
                pairs[(y * width) + x] = new Vector2((float)halfs[x * 2], (float)halfs[(x * 2) + 1]);
            }
        }

        return pairs;
    }

    /// <summary>Reads back an Rgba16Float texture the last frame left in the ShaderRead state, such as a denoiser's guide, top row first.</summary>
    public Vector4[] ReadHalfs(ITexture texture)
    {
        IGraphicsDevice device = BuildDevice();
        TextureDescription description = texture.Description;
        int pitch = description.Format.GetCopyRowPitch(description.Width);
        BufferDescription readbackDescription = new((long)pitch * description.Height, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();
        commands.Begin();
        commands.Barrier(texture, ResourceState.CopySource);
        commands.CopyTextureToBuffer(texture, readback, 0);
        commands.Barrier(texture, ResourceState.ShaderRead);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        Vector4[] texels = new Vector4[description.Width * description.Height];
        byte[] row = new byte[description.Format.GetRowSize(description.Width)];
        for (int y = 0; y < description.Height; y++)
        {
            readback.Read(row, (long)y * pitch);
            ReadOnlySpan<Half> halfs = MemoryMarshal.Cast<byte, Half>(row);
            for (int x = 0; x < description.Width; x++)
            {
                Vector4 texel = new((float)halfs[x * 4], (float)halfs[(x * 4) + 1], (float)halfs[(x * 4) + 2], (float)halfs[(x * 4) + 3]);
                texels[(y * description.Width) + x] = texel;
            }
        }

        return texels;
    }

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

    private uint[] Render(int size, Action<SceneRenderer, IGraphicsDevice> before)
    {
        IGraphicsDevice device = BuildDevice();
        _renderer ??= BuildRenderer(device);
        _configure(_renderer);
        before(_renderer, device);

        TextureDescription colorDescription = new(size, size, Format, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture color = device.CreateTexture(colorDescription);
        int pitch = Format.GetCopyRowPitch(size);
        BufferDescription readbackDescription = new(pitch * size, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        _renderer.Render(commands, _game.World, color);
        commands.Barrier(color, ResourceState.CopySource);
        commands.CopyTextureToBuffer(color, readback, 0);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();

        uint[] pixels = new uint[size * size];
        byte[] row = new byte[size * 4];
        for (int y = 0; y < size; y++)
        {
            readback.Read(row, (long)y * pitch);
            Span<uint> destination = pixels.AsSpan(y * size, size);
            MemoryMarshal.Cast<byte, uint>(row).CopyTo(destination);
        }

        return pixels;
    }

    private SceneRenderer BuildRenderer(IGraphicsDevice device)
    {
        ContentManager content = new(_contentFolder);
        LightingMood mood = new()
        {
            SunDirection = -Vector3.UnitZ,
            Ambient = 1,
            AmbientOcclusion = 0,
            ContactShadowLength = 0,
            Tonemapper = Tonemapper.None,
            Canvas = _paper,
        };
        return new SceneRenderer(device, content, Format) { Mood = mood };
    }

    private IGraphicsDevice BuildDevice()
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

        if (_features.HasFlag(GraphicsFeatures.RayQueries) && !_device.Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            Assert.Skip($"{_device.AdapterName} has no ray queries.");
        }

        if (_upscaling != GraphicsFeatures.None)
        {
            _upscalingDevice = new UpscalingDevice(_device, _upscaling);
            _device = _upscalingDevice;
        }

        return _device;
    }
}
