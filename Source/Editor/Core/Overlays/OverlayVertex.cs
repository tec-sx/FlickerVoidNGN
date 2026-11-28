using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Editor.Core.Overlays;

/// <summary>
/// A corner of an overlay triangle: a world position, how it is drawn and a linear colour with alpha; mirrors what Overlay.hlsl reads.
/// The style is 0 for a plain triangle, <see cref="Hatch"/> for one filled with slanted stripes on the screen, and for a line's strip
/// the corner's distance across the line in pixels, a pixel past its edge on either side, so the shader fades the edge.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct OverlayVertex(Vector3 Position, float Style, Vector4 Color)
{
    /// <summary>The style of a triangle filled with slanted stripes.</summary>
    public const float Hatch = 1000;

    public OverlayVertex(Vector3 position, Vector4 color)
        : this(position, 0, color)
    {
    }
}
