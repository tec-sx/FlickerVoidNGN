using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// What a traced ray hit first: the number of the draw, where the mesh of the frame's draw record with that number stands (none
/// when the ray missed) and how far along the ray, or <see cref="MissedDraw"/>.
/// </summary>
internal readonly record struct RayHit(uint Draw, Vector3? DrawPosition, float Distance)
{
    public const uint MissedDraw = uint.MaxValue;

    public bool Missed => Draw == MissedDraw;
}
