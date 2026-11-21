using Avalonia.Controls;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Ui.Drawing;
using FlickerVoid.Engine.Ui.Surfaces;

using Silk.NET.Maths;

namespace FlickerVoid.Engine.Ui;

/// <summary>
/// The game's interface, Avalonia controls drawn over the scene each frame at the window's size, with the window's mouse and keys
/// handed to them; while the pointer is over a control it keeps the mouse's buttons from the game. Start <see cref="UiPlatform"/> first.
/// </summary>
public sealed class GameUi : IFrameOverlay, IPointerCapture
{
    private readonly IGameWindow _window;
    private readonly UiRenderer _renderer;

    public GameUi(IGraphicsDevice device, IGameWindow window, PixelFormat targetFormat, Control content)
    {
        _window = window;
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Ui");
        _renderer = new UiRenderer(device, shaders, targetFormat);
        Vector2D<int> size = window.Size;
        Surface = new UiSurface(content, Math.Max(size.X, 1), Math.Max(size.Y, 1));
    }

    public UiSurface Surface { get; }

    /// <summary>Whether the interface is drawn and given input.</summary>
    public bool Visible { get; set; } = true;

    public bool CapturesPointer => Visible && Surface.CapturesPointer;

    public void Update(float deltaSeconds)
    {
        if (!Visible)
        {
            return;
        }

        Vector2D<int> size = _window.Size;
        if (size.X > 0 && size.Y > 0)
        {
            Surface.Resize(size.X, size.Y);
        }

        Surface.Update(_window.Input);
    }

    public void Draw(ICommandList commands, ITexture target)
    {
        if (!Visible)
        {
            return;
        }

        _renderer.Draw(commands, target, Surface);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        Surface.Dispose();
    }
}
