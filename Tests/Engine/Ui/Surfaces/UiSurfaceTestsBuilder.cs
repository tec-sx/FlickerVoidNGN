using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Ui.Surfaces;

namespace FlickerVoid.Engine.Ui.Tests.Surfaces;

/// <summary>Builds a 320 by 200 surface over a fake keyboard and mouse, holding a red panel at the top left and a button at the bottom right.</summary>
internal sealed class UiSurfaceTestsBuilder : IDisposable
{
    public const int Width = 320;
    public const int Height = 200;

    private UiSurface? _surface;

    public InputState Input { get; } = new();

    public Border Panel { get; } = new()
    {
        Width = 100,
        Height = 60,
        Background = Brushes.Red,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    public Button Button { get; } = new()
    {
        Content = "Take",
        Width = 80,
        Height = 40,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
    };

    public int Clicks { get; private set; }

    public UiSurface Build()
    {
        UiPlatform.Start<TestApplication>();
        Button.Click += (_, _) => Clicks++;
        Grid root = new() { Children = { Panel, Button } };
        _surface = new UiSurface(root, Width, Height);
        return _surface;
    }

    /// <summary>The surface's pixels, RGBA with premultiplied alpha, rows tightly packed.</summary>
    public byte[] Pixels(UiSurface surface)
    {
        byte[] pixels = new byte[Width * Height * 4];
        surface.Render(pixels, Width * 4);
        return pixels;
    }

    public static (byte R, byte G, byte B, byte A) At(byte[] pixels, int x, int y)
    {
        int offset = ((y * Width) + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }

    public void Dispose()
    {
        _surface?.Dispose();
    }
}
