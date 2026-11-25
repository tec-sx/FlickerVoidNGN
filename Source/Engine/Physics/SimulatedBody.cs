using System.Numerics;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics;

/// <summary>The body an entity has in the simulation, and the entity's pose it was last placed at, so idle bodies cost no call into Jolt.</summary>
internal struct SimulatedBody
{
    public Jolt.BodyID Id;

    public Vector3 PlacedPosition;

    public Quaternion PlacedRotation;

    /// <summary>Whether a kinematic body was moved in the last step, so the next one stops it if its entity stood still.</summary>
    public bool Moving;
}
