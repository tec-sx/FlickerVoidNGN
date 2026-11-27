using Avalonia.Input;

using FlickerVoid.Editor.Core.Cameras;

namespace FlickerVoid.Editor.Viewports;

/// <summary>Maps Avalonia's keys to the keys the viewport acts on.</summary>
public static class ViewportKeys
{
    /// <summary>The viewport key for an Avalonia key, or null when the viewport ignores it.</summary>
    public static ViewportKey? From(Key key) => key switch
    {
        Key.W => ViewportKey.W,
        Key.A => ViewportKey.A,
        Key.S => ViewportKey.S,
        Key.D => ViewportKey.D,
        Key.Q => ViewportKey.Q,
        Key.E => ViewportKey.E,
        Key.R => ViewportKey.R,
        Key.G => ViewportKey.G,
        Key.F => ViewportKey.F,
        Key.Delete => ViewportKey.Delete,
        Key.Escape => ViewportKey.Escape,
        Key.Tab => ViewportKey.Tab,
        Key.D1 => ViewportKey.One,
        Key.D2 => ViewportKey.Two,
        Key.D3 => ViewportKey.Three,
        Key.I => ViewportKey.I,
        Key.C => ViewportKey.C,
        Key.B => ViewportKey.B,
        Key.X => ViewportKey.X,
        Key.L => ViewportKey.L,
        Key.M => ViewportKey.M,
        Key.Enter => ViewportKey.Enter,
        _ => null,
    };
}
