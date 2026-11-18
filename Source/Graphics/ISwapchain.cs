namespace FlickerVoid.Engine.Graphics;

/// <summary>
/// Back buffers shown in a window in turn, synchronized to the display. Each frame, acquire a back buffer, render to it,
/// move it to the Present state, submit, then present.
/// </summary>
public interface ISwapchain : IDisposable
{
    int Width { get; }

    int Height { get; }

    PixelFormat Format { get; }

    /// <summary>Returns the next free back buffer in the Undefined state; its previous contents are gone.</summary>
    ITexture AcquireNextTexture();

    /// <summary>Shows the acquired back buffer once the GPU has finished every command list submitted so far; it must be in the Present state.</summary>
    void Present();

    /// <summary>Recreates the back buffers at the window's new size. Call it after the window resizes, between frames.</summary>
    void Resize(int width, int height);
}
