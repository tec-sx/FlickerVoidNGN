using Arch.System;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Shaders;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>
/// Renders a turning textured cube to the window each frame, following the window's size.
/// Escape or the frame limit closes the window.
/// </summary>
public sealed class MeshRenderSystem : ISystem<GameTime>
{
    private const int FramesInFlight = 3;
    private const float RadiansPerSecond = 0.6f;

    private readonly IGameWindow _window;
    private readonly IGraphicsDevice _device;
    private readonly int _frameLimit;
    private readonly ISwapchain _swapchain;
    private readonly CubeRenderer _cube;
    private readonly ICommandList[] _commandLists = new ICommandList[FramesInFlight];
    private ITexture _depth;
    private float _angle;
    private bool _disposed;

    public MeshRenderSystem(IGameWindow window, IGraphicsDevice device, IShaderCompiler compiler, int frameLimit)
    {
        _window = window;
        _device = device;
        _frameLimit = frameLimit;
        Vector2D<int> size = window.Size;
        SwapchainDescription swapchainDescription = new(window.Native, size.X, size.Y) { Name = "Window" };
        _swapchain = device.CreateSwapchain(swapchainDescription);
        _cube = new CubeRenderer(device, compiler, _swapchain.Format);
        _depth = _cube.CreateDepth(_swapchain.Width, _swapchain.Height);

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
        _angle += (float)t.Elapsed.TotalSeconds * RadiansPerSecond;
        Vector2D<int> size = _window.Size;
        bool visible = size.X > 0 && size.Y > 0;
        if (visible && (size.X != _swapchain.Width || size.Y != _swapchain.Height))
        {
            _swapchain.Resize(size.X, size.Y);
            _depth.Dispose();
            _depth = _cube.CreateDepth(_swapchain.Width, _swapchain.Height);
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

        _cube.Dispose();
        _depth.Dispose();
        _swapchain.Dispose();
    }

    private void Render()
    {
        ITexture backBuffer = _swapchain.AcquireNextTexture();
        ICommandList commands = _commandLists[Frames % FramesInFlight];
        commands.Begin();
        _cube.Record(commands, backBuffer, _depth, _angle);
        commands.Barrier(backBuffer, ResourceState.Present);
        commands.End();
        _device.Submit(commands);
        _swapchain.Present();
    }
}
