using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// How wholly a mesh part takes one depth for the depth tone, so small objects darken or fade as one, at their box's centre, while
/// big ones such as the ground tone pixel by pixel: by how deep its box runs along the view.
/// </summary>
public static class ObjectDepth
{
    /// <summary>How deep along the view, in world units, a box may run and still tone whole; twice as deep, it tones pixel by pixel.</summary>
    public const float WholeSize = 2;

    /// <summary>1 for a box no deeper along the view than <see cref="WholeSize"/>, falling evenly to 0 at twice as deep.</summary>
    public static float Wholeness(in Bounds bounds, Vector3 viewDirection)
    {
        Vector3 along = Vector3.Abs(viewDirection);
        Vector3 extents = bounds.Extents;
        float depth = 2 * Vector3.Dot(along, extents);
        float past = (depth - WholeSize) / WholeSize;
        return 1 - Math.Clamp(past, 0, 1);
    }
}
