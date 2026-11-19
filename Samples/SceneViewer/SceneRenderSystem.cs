using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>Draws the world to the window each frame with a scene renderer, following the window's size; Escape or the frame limit closes the window.</summary>
public sealed class SceneRenderSystem : ISystem<GameTime>
{
    private const int FramesInFlight = 3;

    private readonly IGameWindow _window;
    private readonly IGraphicsDevice _device;
    private readonly World _world;
    private readonly SceneRenderer _renderer;
    private readonly int _frameLimit;
    private readonly ISwapchain _swapchain;
    private readonly ICommandList[] _commandLists = new ICommandList[FramesInFlight];
    private ITexture _depth;
    private bool _disposed;

    public SceneRenderSystem(IGameWindow window, IGraphicsDevice device, World world, Func<PixelFormat, SceneRenderer> createRenderer, int frameLimit)
    {
        _window = window;
        _device = device;
        _world = world;
        _frameLimit = frameLimit;
        Vector2D<int> size = window.Size;
        SwapchainDescription swapchainDescription = new(window.Native, size.X, size.Y) { Name = "Window" };
        _swapchain = device.CreateSwapchain(swapchainDescription);
        _renderer = createRenderer(_swapchain.Format);
        _depth = CreateDepth();

        for (int index = 0; index < FramesInFlight; index++)
        {
            _commandLists[index] = device.CreateCommandList();
        }
    }

    public int Frames { get; private set; }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        Frames++;
        Vector2D<int> size = _window.Size;
        bool visible = size.X > 0 && size.Y > 0;
        if (visible && (size.X != _swapchain.Width || size.Y != _swapchain.Height))
        {
            _swapchain.Resize(size.X, size.Y);
            _depth.Dispose();
            _depth = CreateDepth();
        }

        if (visible)
        {
            Render();
        }

        bool limitReached = _frameLimit > 0 && Frames >= _frameLimit;
        if (_window.Input.WasPressed(Key.Escape) || limitReached)
        {
            _window.Close();
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _device.WaitIdle();
        foreach (ICommandList commands in _commandLists)
        {
            commands.Dispose();
        }

        _renderer.Dispose();
        _depth.Dispose();
        _swapchain.Dispose();
    }

    private ITexture CreateDepth()
    {
        TextureDescription description = new(_swapchain.Width, _swapchain.Height, SceneRenderer.DepthFormat, TextureUsage.DepthStencil) { Name = "Depth" };
        return _device.CreateTexture(description);
    }

    private void Render()
    {
        ITexture backBuffer = _swapchain.AcquireNextTexture();
        ICommandList commands = _commandLists[Frames % FramesInFlight];
        commands.Begin();
        _renderer.Render(commands, _world, backBuffer, _depth);
        commands.Barrier(backBuffer, ResourceState.Present);
        commands.End();
        _device.Submit(commands);
        _swapchain.Present();
    }
}
