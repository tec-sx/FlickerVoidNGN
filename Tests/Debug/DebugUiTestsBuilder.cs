using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Engine.Debug.Tests;

/// <summary>
/// Builds a debug UI over a fake window and a Vulkan device with validation on, skipping the test without Vulkan 1.3, and
/// renders its frames into a small Rgba8 target read back as pixels. Disposing it fails the test on any logged error.
/// </summary>
internal sealed class DebugUiTestsBuilder : IDisposable
{
    public const int Width = 320;
    public const int Height = 120;

    /// <summary>The color the target is cleared to, which is not any color the UI draws.</summary>
    public static readonly uint Background = 0xFFFF00FF;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly InputState _input = new();
    private readonly IGameWindow _window = Substitute.For<IGameWindow>();
    private readonly Dictionary<string, int> _renders = [];
    private readonly string _settingsDirectory;
    private readonly bool _ownsSettings;
    private IGraphicsDevice? _device;
    private DebugUi? _ui;
    private Game? _game;
    private SceneRenderer? _renderer;
    private bool _engineWindows;
    private bool _renderingWindows;

    public DebugUiTestsBuilder()
        : this(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), ownsSettings: true)
    {
    }

    private DebugUiTestsBuilder(string settingsDirectory, bool ownsSettings)
    {
        _settingsDirectory = settingsDirectory;
        _ownsSettings = ownsSettings;
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _window.Input.Returns(_input);
        _window.Size.Returns(new Vector2D<int>(Width, Height));
    }

    public DebugUi Ui => _ui ?? throw new InvalidOperationException("Render a frame first.");

    public DebugUiTestsBuilder WithWindow(string title, string category, bool open = false)
    {
        Windows.Add(new ActionWindow(title, category, () => Count(title)) { IsOpen = open });
        return this;
    }

    /// <summary>Adds the engine's own windows, open, over a game with a point and a spot light.</summary>
    public DebugUiTestsBuilder WithEngineWindows()
    {
        _engineWindows = true;
        return this;
    }

    public DebugUiTestsBuilder WithRenderingWindows()
    {
        _renderingWindows = true;
        return this;
    }

    public SceneRenderer? Renderer => _renderer;

    public int Renders(string title) => _renders.GetValueOrDefault(title);

    public void Press(Key key) => _input.PressKey(key);

    public void MoveMouse(float x, float y) => _input.MoveMouse(new System.Numerics.Vector2(x, y));

    /// <summary>A second debug UI over the same settings folder, as the next run of a program would have.</summary>
    public DebugUiTestsBuilder Another() => new(_settingsDirectory, ownsSettings: false);

    /// <summary>Runs one frame of the UI, drawn over a cleared target, and reads the target back as Rgba8 pixels, top row first.</summary>
    public uint[] RenderFrame(bool visible)
    {
        IGraphicsDevice device = BuildDevice();
        DebugUi ui = BuildUi(device);
        if (!_input.IsDown(Key.F1) && !_input.WasPressed(Key.F1))
        {
            ui.Visible = visible;
        }

        ui.Update(1f / 60);

        TextureDescription description = new(Width, Height, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture color = device.CreateTexture(description);
        int pitch = PixelFormat.Rgba8UNorm.GetCopyRowPitch(Width);
        BufferDescription readbackDescription = new(pitch * Height, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(color, ResourceState.RenderTarget);
        commands.BeginRendering([new ColorTarget(color, LoadAction.Clear, new System.Numerics.Vector4(1, 0, 1, 1))]);
        commands.EndRendering();
        ui.Draw(commands, color);
        commands.Barrier(color, ResourceState.CopySource);
        commands.CopyTextureToBuffer(color, readback, 0);
        commands.End();
        device.Submit(commands);
        device.WaitIdle();
        _input.EndFrame();

        uint[] pixels = new uint[Width * Height];
        byte[] row = new byte[Width * 4];
        for (int y = 0; y < Height; y++)
        {
            readback.Read(row, (long)y * pitch);
            MemoryMarshal.Cast<byte, uint>(row).CopyTo(pixels.AsSpan(y * Width, Width));
        }

        return pixels;
    }

    /// <summary>Renders the given number of frames, since ImGui shows a new window only from its second frame, and returns the last.</summary>
    public uint[] RenderAndDispose(bool visible, int frames = 1)
    {
        uint[] pixels = [];
        for (int frame = 0; frame < frames; frame++)
        {
            pixels = RenderFrame(visible);
        }

        Dispose();
        return pixels;
    }

    public void Dispose()
    {
        _ui?.Dispose();
        _renderer?.Dispose();
        _game?.Dispose();
        _device?.Dispose();
        _loggerFactory.Dispose();
        if (_ownsSettings && Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }

        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    private readonly DebugWindows _pending = new();

    private DebugWindows Windows => _ui?.Windows ?? _pending;

    private void Count(string title)
    {
        _renders[title] = _renders.GetValueOrDefault(title) + 1;
    }

    private DebugUi BuildUi(IGraphicsDevice device)
    {
        if (_ui is not null)
        {
            return _ui;
        }

        _ui = new DebugUi(device, _window, PixelFormat.Rgba8UNorm, _settingsDirectory);
        foreach (DebugWindow window in _pending.All)
        {
            _ui.Windows.Add(window);
        }

        AddEngineWindows(device);
        _ui.LoadLayout();
        return _ui;
    }

    private void AddEngineWindows(IGraphicsDevice device)
    {
        if (!_engineWindows && !_renderingWindows)
        {
            return;
        }

        _game = new Game(_loggerFactory);
        _game.AddRendering();
        _game.World.Create(new Transform(), new PointLight { CastsShadows = true });
        _game.World.Create(new Transform(), new SpotLight());
        if (_engineWindows)
        {
            _ui!.AddEngineWindows(_game, device);
        }

        if (_renderingWindows)
        {
            _renderer = new SceneRenderer(device, new ContentManager(_settingsDirectory), PixelFormat.Rgba8UNorm);
            _ui!.AddRenderingWindows(_renderer, _game.World);
        }

        foreach (DebugWindow window in _ui!.Windows.All)
        {
            window.IsOpen = true;
        }
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
