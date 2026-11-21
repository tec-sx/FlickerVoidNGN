using Avalonia.Input;

namespace FlickerVoid.Engine.Ui.Surfaces;

/// <summary>The keys the game's interface reacts to, from the window's keys to Avalonia's; typed text arrives separately.</summary>
internal static class UiKeys
{
    public static readonly (Silk.NET.Input.Key Silk, Key Avalonia)[] Map =
    [
        (Silk.NET.Input.Key.Escape, Key.Escape),
        (Silk.NET.Input.Key.Enter, Key.Enter),
        (Silk.NET.Input.Key.Space, Key.Space),
        (Silk.NET.Input.Key.Tab, Key.Tab),
        (Silk.NET.Input.Key.Backspace, Key.Back),
        (Silk.NET.Input.Key.Delete, Key.Delete),
        (Silk.NET.Input.Key.Left, Key.Left),
        (Silk.NET.Input.Key.Right, Key.Right),
        (Silk.NET.Input.Key.Up, Key.Up),
        (Silk.NET.Input.Key.Down, Key.Down),
        (Silk.NET.Input.Key.Home, Key.Home),
        (Silk.NET.Input.Key.End, Key.End),
        (Silk.NET.Input.Key.Number1, Key.D1),
        (Silk.NET.Input.Key.Number2, Key.D2),
        (Silk.NET.Input.Key.Number3, Key.D3),
    ];
}
