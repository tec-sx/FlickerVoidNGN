using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>A line of its color drawn around its entity's mesh, over whatever stands in front of it, such as a highlight on what can be used.</summary>
public struct Outline
{
    public Outline()
    {
    }

    [Color]
    public Vector3 Color { get; set; } = Vector3.One;
}
