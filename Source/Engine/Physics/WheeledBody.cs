using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Physics;

/// <summary>
/// A dynamic body that drives on wheels: its collider is the chassis, and each wheel hangs under it on a suspension, casts onto the
/// ground and pushes the body as the engine, the brakes and the steering say (<see cref="DriveInput"/> on the same entity). It
/// faces +Z with its left at +X. The wheels are not bodies; their poses are read with <see cref="IPhysicsSystem.TryGetWheelPose"/>.
/// </summary>
public struct WheeledBody
{
    public WheeledBody()
    {
    }

    public required Collider Collider { get; set; }

    public float Mass { get; set; } = 1500;

    public BodyWheel[] Wheels { get; set; } = [];

    /// <summary>The engine's greatest torque, in newton metres.</summary>
    public float EngineTorque { get; set; } = 500;

    /// <summary>The brakes' greatest torque on each wheel, in newton metres.</summary>
    public float BrakeTorque { get; set; } = 1500;

    /// <summary>How far each wheel travels on its suspension, in metres, half up and half down from where it rests.</summary>
    public float SuspensionTravel { get; set; } = 0.3f;

    /// <summary>How fast the suspension springs back, in cycles a second; softer is lower.</summary>
    public float SuspensionFrequency { get; set; } = 1.5f;

    /// <summary>How quickly the suspension stops bouncing, from 0 (never) to 1 (at once).</summary>
    public float SuspensionDamping { get; set; } = 0.5f;

    /// <summary>How far under the collider's centre the weight sits, in metres, which keeps the body from rolling over.</summary>
    public float WeightDrop { get; set; } = 0.3f;

    /// <summary>Whether the body leans into turns to stay up, as a motorcycle on two wheels in line does.</summary>
    public bool Leans { get; set; }
}
