using System.Numerics;

namespace FlickerVoid.Engine.Physics;

/// <summary>One wheel of a <see cref="WheeledBody"/>: where its centre rests in the entity's space, its size, and what it does.</summary>
public sealed class BodyWheel
{
    /// <summary>The wheel's centre at rest, in the entity's space; the suspension lets it travel up and down from there.</summary>
    public Vector3 Position { get; set; }

    public float Radius { get; set; } = 0.35f;

    public float Width { get; set; } = 0.25f;

    /// <summary>How far the wheel turns at full steering, in degrees; 0 for a wheel that doesn't steer.</summary>
    public float MaxSteer { get; set; }

    /// <summary>Whether the engine turns the wheel.</summary>
    public bool Driven { get; set; }

    /// <summary>Whether the hand brake holds the wheel.</summary>
    public bool HandBrake { get; set; }
}
