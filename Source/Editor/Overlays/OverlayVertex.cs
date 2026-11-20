using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Editor.Overlays;

/// <summary>A corner of an overlay triangle: a world position and a linear colour with alpha; mirrors what Overlay.hlsl reads.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct OverlayVertex(Vector3 Position, float Padding, Vector4 Color)
{
    public OverlayVertex(Vector3 position, Vector4 color)
        : this(position, 0, color)
    {
    }
}
