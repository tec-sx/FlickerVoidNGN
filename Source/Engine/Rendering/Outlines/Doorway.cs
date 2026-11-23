using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>A way into or out of a room: a box standing on its entity, drawn only as an outline of its color, seen through walls.</summary>
public struct Doorway
{
    public Doorway()
    {
    }

    /// <summary>Width, height and depth; the box stands on the entity's position, centred across.</summary>
    public Vector3 Size { get; set; } = new(1.2f, 2.2f, 0.4f);

    [Color]
    public Vector3 Color { get; set; } = new(1, 1, 0);
}
