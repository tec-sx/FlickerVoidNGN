namespace FlickerVoid.Engine.Graphics.Tests.AccelerationStructures;

/// <summary>What a traced ray hit first: the instance's id and how far along the ray, or <see cref="MissedId"/>.</summary>
internal readonly record struct RayHit(uint Instance, float Distance)
{
    public const uint MissedId = uint.MaxValue;

    public bool Missed => Instance == MissedId;
}
