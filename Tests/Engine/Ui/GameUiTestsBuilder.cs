using System.Numerics;
using System.Runtime.InteropServices;

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Ui.Surfaces;
using FlickerVoid.Engine.Ui.Tests.Surfaces;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Silk.NET.Maths;

namespace FlickerVoid.Engine.Ui.Tests;

/// <summary>
/// Builds a game interface holding a red panel at the top left over a fake window and a Vulkan device with validation on, skipping
/// the test without Vulkan 1.3, and draws it over a target cleared to the scene's colour, read back as Rgba8 pixels.
/// </summary>
internal sealed class GameUiTestsBuilder : IDisposable
{
    public const int Width = 320;
    public const int Height = 200;

    /// <summary>The colour the target is cleared to, standing in for the scene: opaque blue.</summary>
    public const uint Scene = 0xFFFF0000;

    private readonly RecordingLoggerProvider _logs = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly InputState _input = new();
    private readonly IGameWindow _window = Substitute.For<IGameWindow>();
    private IGraphicsDevice? _device;
    private GameUi? _ui;

    public GameUiTestsBuilder()
    {
        _loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(_logs));
        _window.Input.Returns(_input);
        _window.Size.Returns(new Vector2D<int>(Width, Height));
    }

    public GameUi Ui => _ui ?? BuildUi();

    /// <summary>The red panel at the top left.</summary>
    public Border Panel { get; } = new()
    {
        Width = 100,
        Height = 60,
        Background = Brushes.Red,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    public void MoveMouse(Vector2 position) => _input.MoveMouse(position);

    public void ResizeWindow(int width, int height) => _window.Size.Returns(new Vector2D<int>(width, height));

    /// <summary>The pixel at a point of a frame, as Rgba8 packed little end first.</summary>
    public static uint At(uint[] pixels, int x, int y) => pixels[(y * Width) + x];

    /// <summary>Runs one frame of the interface, drawn over the cleared target, and reads the target back, top row first.</summary>
    public uint[] RenderFrame()
    {
        GameUi ui = Ui;
        IGraphicsDevice device = _device!;
        ui.Update(1f / 60);

        TextureDescription description = new(Width, Height, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource);
        using ITexture color = device.CreateTexture(description);
        int pitch = PixelFormat.Rgba8UNorm.GetCopyRowPitch(Width);
        BufferDescription readbackDescription = new(pitch * Height, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(color, ResourceState.RenderTarget);
        commands.BeginRendering([new ColorTarget(color, LoadAction.Clear, new Vector4(0, 0, 1, 1))]);
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

    public void Dispose()
    {
        _ui?.Dispose();
        _device?.Dispose();
        _loggerFactory.Dispose();
        _logs.Errors.Should().BeEmpty("the device logged no errors");
    }

    private GameUi BuildUi()
    {
        IGraphicsDevice device = BuildDevice();
        UiPlatform.Start<TestApplication>();
        Grid root = new() { Children = { Panel } };
        _ui = new GameUi(device, _window, PixelFormat.Rgba8UNorm, root);
        return _ui;
    }

    private IGraphicsDevice BuildDevice()
    {
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
