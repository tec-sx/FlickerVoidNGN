using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Presentation;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Platform.Native;

namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>
/// Creates the device under test like <see cref="GraphicsDeviceTestsBuilder"/>, opens a hidden window and a swapchain for it,
/// and renders frames that are read back before they are presented. Skips the test when the machine has no display.
/// </summary>
internal sealed class SwapchainTestsBuilder : IDisposable
{
    public const int WindowWidth = 64;
    public const int WindowHeight = 32;

    private readonly GraphicsDeviceTestsBuilder _deviceBuilder;
    private IWindow? _window;

    static SwapchainTestsBuilder()
    {
        NativeLibraryPaths.AddRuntimesFolder();
        SdlWindowing.RegisterPlatform();
    }

    public SwapchainTestsBuilder()
    {
        _deviceBuilder = new GraphicsDeviceTestsBuilder();
    }

    public IGraphicsDevice Build() => _deviceBuilder.Build();

    public ISwapchain CreateSwapchain(IGraphicsDevice device)
    {
        _window = OpenWindow();
        SwapchainDescription description = new(_window.Native!, WindowWidth, WindowHeight) { Name = "Test swapchain" };
        try
        {
            return device.CreateSwapchain(description);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }
    }

    public void ResizeWindow(int width, int height)
    {
        _window!.Size = new Vector2D<int>(width, height);
        _window.DoEvents();
    }

    /// <summary>Acquires a back buffer, clears it to the color, copies it to the CPU, presents it and returns its pixels.</summary>
    public uint[] RenderFrame(IGraphicsDevice device, ISwapchain swapchain, Vector4 color)
    {
        ITexture backBuffer = swapchain.AcquireNextTexture();
        TextureDescription description = backBuffer.Description;
        int pitch = description.Format.GetCopyRowPitch(description.Width);
        BufferDescription readbackDescription = new((long)pitch * description.Height, BufferUsage.CopyDestination, MemoryLocation.Readback);
        using IBuffer readback = device.CreateBuffer(readbackDescription);
        using ICommandList commands = device.CreateCommandList();

        commands.Begin();
        commands.Barrier(backBuffer, ResourceState.RenderTarget);
        ColorTarget target = new(backBuffer, LoadAction.Clear, color);
        commands.BeginRendering([target]);
        commands.EndRendering();
        commands.Barrier(backBuffer, ResourceState.CopySource);
        commands.CopyTextureToBuffer(backBuffer, readback, 0);
        commands.Barrier(backBuffer, ResourceState.Present);
        commands.End();
        device.Submit(commands);
        swapchain.Present();
        device.WaitIdle();

        return GraphicsDeviceTestsBuilder.ReadRows(readback, description);
    }

    /// <summary>A color as Bgra8UNorm bytes read back little-endian.</summary>
    public static uint Bgra(Vector4 color)
    {
        Vector4 scaled = color * 255;
        return GraphicsDeviceTestsBuilder.Rgba((byte)scaled.Z, (byte)scaled.Y, (byte)scaled.X, (byte)scaled.W);
    }

    public void Dispose()
    {
        _window?.Dispose();
        _deviceBuilder.Dispose();
    }

    private static IWindow OpenWindow()
    {
        Vector2D<int> size = new(WindowWidth, WindowHeight);
        WindowOptions options = WindowOptions.Default with { Title = "Swapchain test", Size = size, API = GraphicsAPI.None, IsVisible = false };
        IWindow window = Window.Create(options);
        window.Initialize();
        return window;
    }
}
