using Arch.Core;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

using EnginePixelFormat = FlickerVoid.Engine.Graphics.Textures.PixelFormat;

namespace FlickerVoid.Engine.Ui.Stages;

/// <summary>
/// A small scene shown in the interface, such as the protagonist in the hub or an item being inspected: its scene file (models, lights,
/// a camera, a mood) loaded into a world of its own, spun by its turntables and animated, and while a <see cref="StageView"/> shows it, rendered with
/// the game's renderer and read back into <see cref="Bitmap"/> each update.
/// </summary>
public sealed class Stage : IDisposable
{
    private const EnginePixelFormat Format = EnginePixelFormat.Rgba8UNormSrgb;

    private readonly IGraphicsDevice _device;
    private readonly SceneLoader _loader;
    private readonly SceneRenderer _renderer;
    private readonly ICommandList _commands;
    private readonly MeshAnimationSystem _animation;
    private ITexture? _target;
    private IBuffer? _readback;
    private byte[] _row = [];

    public Stage(IGraphicsDevice device, IContentManager content, ISceneSpawner spawner, string scene, ILoggerFactory loggerFactory)
    {
        _device = device;
        ILogger<SceneLoader> logger = loggerFactory.CreateLogger<SceneLoader>();
        _loader = new SceneLoader(spawner, content, World, logger);
        _loader.Load(scene);
        _renderer = new SceneRenderer(device, content, Format);
        string? mood = SceneMood.Find(World);
        if (mood is not null)
        {
            MoodOverride file = content.Load(mood, RenderingJsonContext.Default.MoodOverride);
            _renderer.Mood = file.ToMood();
        }

        _commands = device.CreateCommandList();
        _animation = new MeshAnimationSystem(World);
    }

    public World World { get; } = World.Create();

    /// <summary>Pixels to render at; the view showing the stage sets it.</summary>
    public PixelSize Size { get; set; }

    /// <summary>Set while a view shows the stage; a hidden stage only spins.</summary>
    public bool IsShown { get; set; }

    /// <summary>The last image rendered; null until the stage is first shown.</summary>
    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>Raised after <see cref="Bitmap"/> holds a new image.</summary>
    public event Action? Rendered;

    public void Drag(float pixels) => TurntableSystem.Drag(World, pixels);

    public void Update(float seconds)
    {
        TurntableSystem.Spin(World, seconds);
        TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
        _animation.Update(new GameTime(elapsed, elapsed, 1));
        if (!IsShown || Size.Width <= 0 || Size.Height <= 0)
        {
            return;
        }

        Resize();
        ITexture target = _target!;
        IBuffer readback = _readback!;
        _commands.Begin();
        _renderer.Render(_commands, World, target);
        _commands.Barrier(target, ResourceState.CopySource);
        _commands.CopyTextureToBuffer(target, readback, 0);
        _commands.End();
        _device.Submit(_commands);
        _device.WaitIdle();
        CopyToBitmap(readback);
        Rendered?.Invoke();
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _commands.Dispose();
        _readback?.Dispose();
        _target?.Dispose();
        _renderer.Dispose();
        _loader.Dispose();
        Bitmap?.Dispose();
        World.Destroy(World);
    }

    private void Resize()
    {
        if (Bitmap is not null && Bitmap.PixelSize == Size)
        {
            return;
        }

        _readback?.Dispose();
        _target?.Dispose();
        Bitmap?.Dispose();
        TextureDescription target = new(Size.Width, Size.Height, Format, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = "Stage" };
        _target = _device.CreateTexture(target);
        int pitch = Format.GetCopyRowPitch(Size.Width);
        BufferDescription readback = new(pitch * Size.Height, BufferUsage.CopyDestination, MemoryLocation.Readback);
        _readback = _device.CreateBuffer(readback);
        Bitmap = new WriteableBitmap(Size, new Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, AlphaFormat.Premul);
        _row = new byte[Size.Width * 4];
    }

    private unsafe void CopyToBitmap(IBuffer readback)
    {
        int pitch = Format.GetCopyRowPitch(Size.Width);
        using ILockedFramebuffer frame = Bitmap!.Lock();
        for (int y = 0; y < Size.Height; y++)
        {
            readback.Read(_row, (long)y * pitch);
            byte* destination = (byte*)frame.Address + ((long)y * frame.RowBytes);
            Span<byte> row = new(destination, _row.Length);
            _row.CopyTo(row);
        }
    }
}
