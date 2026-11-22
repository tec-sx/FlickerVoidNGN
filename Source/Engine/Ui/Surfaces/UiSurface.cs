using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FlickerVoid.Engine.Platform.Input;

using MouseButton = Silk.NET.Input.MouseButton;

namespace FlickerVoid.Engine.Ui.Surfaces;

/// <summary>
/// The game's interface as Avalonia controls in a window of their own that is never shown on screen: the game's mouse and keys are
/// handed to it each frame, and it is drawn into memory, transparent where it has no controls, for the frame to draw over the scene.
/// </summary>
public sealed unsafe class UiSurface : IPointerCapture, IDisposable
{
    private static readonly MouseButton[] Buttons = [MouseButton.Left, MouseButton.Right, MouseButton.Middle];

    private readonly Window _window;
    private Vector2 _pointer = new(-1, -1);

    public UiSurface(Control content, int width, int height)
    {
        Content = content;
        _window = new Window
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Content = content,
        };
        _window.Show();
        Width = width;
        Height = height;
    }

    /// <summary>The controls it shows.</summary>
    public Control Content { get; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Whether the pointer is over one of its controls, so the game leaves the mouse's buttons to it.</summary>
    public bool CapturesPointer { get; private set; }

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return;
        }

        Width = width;
        Height = height;
        _window.Width = width;
        _window.Height = height;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Hands the frame's mouse and typed text to the controls and runs Avalonia's queued work.</summary>
    public void Update(IInputState input)
    {
        Point point = new(input.MousePosition.X, input.MousePosition.Y);
        if (input.MousePosition != _pointer)
        {
            _pointer = input.MousePosition;
            _window.MouseMove(point, RawInputModifiers.None);
        }

        foreach (MouseButton button in Buttons)
        {
            Avalonia.Input.MouseButton avaloniaButton = ToAvalonia(button);
            if (input.WasPressed(button))
            {
                _window.MouseDown(point, avaloniaButton, RawInputModifiers.None);
            }

            if (input.WasReleased(button))
            {
                _window.MouseUp(point, avaloniaButton, RawInputModifiers.None);
            }
        }

        if (input.ScrollDelta != 0)
        {
            Avalonia.Vector delta = new(0, input.ScrollDelta);
            _window.MouseWheel(point, delta, RawInputModifiers.None);
        }

        foreach ((Silk.NET.Input.Key key, Key avaloniaKey) in UiKeys.Map)
        {
            if (input.WasPressed(key))
            {
                _window.KeyPress(avaloniaKey, RawInputModifiers.None, PhysicalKey.None, null);
            }

            if (input.WasReleased(key))
            {
                _window.KeyRelease(avaloniaKey, RawInputModifiers.None, PhysicalKey.None, null);
            }
        }

        if (input.TypedText.Length > 0)
        {
            _window.KeyTextInput(input.TypedText);
        }

        Dispatcher.UIThread.RunJobs();
        CapturesPointer = IsOverControl(point);
    }

    /// <summary>Draws the controls into <paramref name="destination"/>: RGBA rows of <paramref name="rowPitch"/> bytes, alpha premultiplied.</summary>
    public void Render(Span<byte> destination, int rowPitch)
    {
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap frame = _window.CaptureRenderedFrame()!;
        using ILockedFramebuffer buffer = frame.Lock();
        int width = Math.Min(Width, buffer.Size.Width);
        int height = Math.Min(Height, buffer.Size.Height);
        int rowBytes = width * 4;
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> row = new((byte*)buffer.Address + ((long)y * buffer.RowBytes), rowBytes);
            Span<byte> target = destination.Slice(y * rowPitch, rowBytes);
            row.CopyTo(target);
        }
    }

    public void Dispose()
    {
        _window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private bool IsOverControl(Point point)
    {
        IInputElement? hit = _window.InputHitTest(point);
        if (hit is not Visual visual)
        {
            return false;
        }

        return visual != Content && Content.IsVisualAncestorOf(visual);
    }

    private static Avalonia.Input.MouseButton ToAvalonia(MouseButton button)
    {
        switch (button)
        {
            case MouseButton.Right:
                return Avalonia.Input.MouseButton.Right;
            case MouseButton.Middle:
                return Avalonia.Input.MouseButton.Middle;
            default:
                return Avalonia.Input.MouseButton.Left;
        }
    }
}
