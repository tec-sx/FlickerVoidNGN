using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Presentation;

/// <summary>
/// Back buffers shown in a window in turn, synchronized to the display only with <see cref="VSync"/> on. Each frame, acquire a back buffer, render to it,
/// move it to the Present state, submit, then present.
/// </summary>
public interface ISwapchain : IDisposable
{
    int Width { get; }

    int Height { get; }

    PixelFormat Format { get; }

    /// <summary>
    /// Whether presenting waits for the display's refresh (FIFO). Off, frames are shown at once (mailbox where the display offers it,
    /// otherwise immediate, which may tear), so the frame rate is not capped at the refresh rate. A change applies at the next acquire.
    /// </summary>
    bool VSync { get; set; }

    /// <summary>Returns the next free back buffer in the Undefined state; its previous contents are gone.</summary>
    ITexture AcquireNextTexture();

    /// <summary>Shows the acquired back buffer once the GPU has finished every command list submitted so far; it must be in the Present state.</summary>
    void Present();

    /// <summary>Recreates the back buffers at the window's new size. Call it after the window resizes, between frames.</summary>
    void Resize(int width, int height);
}

