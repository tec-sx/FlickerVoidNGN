using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>What a painter draws an item with: the lane's colour, the theme's colours, the pulse of queued things and the pixels a unit takes.</summary>
public sealed class LanePaint
{
    public required Color Tint { get; init; }

    public required Color Panel { get; init; }

    public required Color Text { get; init; }

    public required Color MutedText { get; init; }

    public required Color Hint { get; init; }

    public required Color Ink { get; init; }

    /// <summary>From 0 to 1, brightest on the beat, for what waits to start.</summary>
    public required double Pulse { get; init; }

    public required double PixelsPerUnit { get; init; }
}
