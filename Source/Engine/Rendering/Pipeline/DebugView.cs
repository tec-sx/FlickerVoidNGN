namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>What the scene renderer shows: the lit frame, or one of the G-buffer's targets or the depth in its place, to check them by eye.</summary>
public enum DebugView
{
    Lit,

    /// <summary>Each surface's color before any light.</summary>
    Albedo,

    /// <summary>The world normal, each axis shown as half of it plus half, so +Z is lavender blue.</summary>
    Normal,

    /// <summary>Each material in a color of its own, lightened where the paint stands thick.</summary>
    Material,

    /// <summary>The depth the depth tone reads, grey as depth over depth plus 10, so small objects show flat.</summary>
    ObjectDepth,

    /// <summary>How far each pixel moved since the frame before: middle grey when still, red and green across and down, full at 8 pixels.</summary>
    Motion,

    /// <summary>Each pixel's own view depth, grey as in <see cref="ObjectDepth"/>.</summary>
    Depth,
}
