using System.Numerics;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// A kinematic joint on a creation's part, a point with no mesh in the space of the entity placed in the level: a hinge turns the
/// part about <see cref="Axis"/> through <see cref="Pivot"/> by degrees, a slider moves it along <see cref="Axis"/> by a distance;
/// its position stays between <see cref="Min"/> and <see cref="Max"/> and moves toward its target at <see cref="Speed"/> a second
/// (at once when not above zero).
/// </summary>
public record struct PartJoint
{
    public JointKind Kind { get; init; }

    public Vector3 Pivot { get; init; }

    public Vector3 Axis { get; init; }

    public float Min { get; init; }

    public float Max { get; init; }

    public float Speed { get; init; }

    /// <summary>Where a door's joint stands open: the limit farther from rest, so a door hinged to open the other way has a negative one.</summary>
    public readonly float GetOpenPosition()
    {
        if (MathF.Abs(Max) >= MathF.Abs(Min))
        {
            return Max;
        }

        return Min;
    }
}
