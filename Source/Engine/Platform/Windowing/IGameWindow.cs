using FlickerVoid.Engine.Platform.Input;

using Silk.NET.Core.Contexts;
using Silk.NET.Maths;

namespace FlickerVoid.Engine.Platform.Windowing;

/// <summary>The game's window: runs the game loop until it closes and provides the frame's keyboard and mouse input.</summary>
public interface IGameWindow
{
    IInputState Input { get; }

    string Title { get; set; }

    /// <summary>The size of the window's drawable area in pixels.</summary>
    Vector2D<int> Size { get; }

    /// <summary>The OS handles of the window, which graphics devices create swapchains for.</summary>
    INativeWindow Native { get; }

    /// <summary>Runs until the window closes: each frame handles the window's events, ticks the game with the real time since the last frame, then ends the input frame. It never sleeps: the renderer's FIFO swapchain paces frames.</summary>
    void Run();

    void Close();
}

