using System.Diagnostics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Presentation;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Loop;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Engine.Hosting;

/// <summary>
/// Draws the world to the window each frame with a scene renderer and an optional overlay, following the window's size, and times the frames;
/// the frame limit closes the window, and so does Escape unless the game takes it (<see cref="CloseOnEscape"/>).
/// </summary>
public sealed class SceneRenderSystem : ISystem<GameTime>
{
    private const int FramesInFlight = SceneRenderer.MaxFramesInFlight;

    private readonly IGameWindow _window;
    private readonly IGraphicsDevice _device;
    private readonly World _world;
    private readonly SceneRenderer _renderer;
    private readonly int _frameLimit;
    private readonly ISwapchain _swapchain;
    private readonly ICommandList[] _commandLists = new ICommandList[FramesInFlight];
    private readonly Stopwatch _clock = new();
    private readonly IFrameOverlay? _overlay;
    private LightingMood? _sourceMood;
    private bool _disposed;

    public SceneRenderSystem(IGameWindow window, IGraphicsDevice device, World world, Func<PixelFormat, SceneRenderer> createRenderer, int frameLimit, Func<PixelFormat, SceneRenderer, IFrameOverlay?> createOverlay)
    {
        _window = window;
        _device = device;
        _world = world;
        _frameLimit = frameLimit;
        Vector2D<int> size = window.Size;
        SwapchainDescription swapchainDescription = new(window.Native, size.X, size.Y) { Name = "Window" };
        _swapchain = device.CreateSwapchain(swapchainDescription);
        _renderer = createRenderer(_swapchain.Format);
        _overlay = createOverlay(_swapchain.Format, _renderer);

        for (int index = 0; index < FramesInFlight; index++)
        {
            _commandLists[index] = device.CreateCommandList();
        }
    }

    public SceneRenderer Renderer => _renderer;

    public IFrameOverlay? Overlay => _overlay;

    public int Frames { get; private set; }

    /// <summary>
    /// Gives the mood to draw each frame with, or null to leave the renderer's alone. The renderer takes the mood when it is another
    /// object than the frame before gave, so a mood tweaked in place, as the debug panel does, stays until the source's changes.
    /// </summary>
    public Func<LightingMood>? MoodSource { get; set; }

    /// <summary>Whether Escape closes the window; a game that gives Escape to its own menus turns it off and closes the window itself.</summary>
    public bool CloseOnEscape { get; set; } = true;

    /// <summary>The mean time between frames after the first, which pays for loading and compiling.</summary>
    public TimeSpan AverageFrameTime
    {
        get
        {
            if (Frames < 2)
            {
                return TimeSpan.Zero;
            }

            return _clock.Elapsed / (Frames - 1);
        }
    }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        Frames++;
        if (Frames == 2)
        {
            _clock.Start();
        }
        Vector2D<int> size = _window.Size;
        bool visible = size.X > 0 && size.Y > 0;
        if (visible && (size.X != _swapchain.Width || size.Y != _swapchain.Height))
        {
            _swapchain.Resize(size.X, size.Y);
        }

        TakeMood();
        _overlay?.Update((float)t.Elapsed.TotalSeconds);
        if (visible)
        {
            Render();
        }

        bool limitReached = _frameLimit > 0 && Frames >= _frameLimit;
        bool escaped = CloseOnEscape && _window.Input.WasPressed(Key.Escape);
        if (escaped || limitReached)
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

        _overlay?.Dispose();
        _renderer.Dispose();
        _swapchain.Dispose();
    }

    private void TakeMood()
    {
        if (MoodSource is null)
        {
            return;
        }

        LightingMood mood = MoodSource();
        if (!ReferenceEquals(mood, _sourceMood))
        {
            _sourceMood = mood;
            _renderer.Mood = mood;
        }
    }

    private void Render()
    {
        ITexture backBuffer = _swapchain.AcquireNextTexture();
        ICommandList commands = _commandLists[Frames % FramesInFlight];
        commands.Begin();
        _renderer.Render(commands, _world, backBuffer);
        if (_overlay is not null)
        {
            commands.BeginTimer("Overlay");
            _overlay.Draw(commands, backBuffer);
            commands.EndTimer();
        }

        commands.Barrier(backBuffer, ResourceState.Present);
        commands.End();
        _device.Submit(commands);
        _swapchain.Present();
    }
}
