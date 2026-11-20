// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See THIRD-PARTY-NOTICES.md.
// Adapted from Stride.BepuPhysics' StridePoseIntegratorCallbacks: one gravity and one damping for every body.

using System.Numerics;

using BepuPhysics;

using BepuUtilities;

namespace FlickerVoid.Engine.Physics;

/// <summary>Applies gravity and damping to the velocity of dynamic bodies.</summary>
internal struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    private Vector3Wide _gravityWideDt;
    private Vector<float> _linearDampingDt;
    private Vector<float> _angularDampingDt;

    public PoseIntegratorCallbacks()
    {
    }

    public Vector3 Gravity { get; set; } = new(0, -9.8f, 0);

    /// <summary>Fraction of linear velocity removed per second, from 0 (none) to 1.</summary>
    public float LinearDamping { get; set; } = 0.05f;

    /// <summary>Fraction of angular velocity removed per second, from 0 (none) to 1.</summary>
    public float AngularDamping { get; set; } = 0.05f;

    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    public readonly bool AllowSubstepsForUnconstrainedBodies => false;

    public readonly bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
        float linearKept = Math.Clamp(1 - LinearDamping, 0, 1);
        float angularKept = Math.Clamp(1 - AngularDamping, 0, 1);
        Vector3 gravityDt = Gravity * dt;
        _linearDampingDt = new Vector<float>(MathF.Pow(linearKept, dt));
        _angularDampingDt = new Vector<float>(MathF.Pow(angularKept, dt));
        _gravityWideDt = Vector3Wide.Broadcast(gravityDt);
    }

    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
    {
        velocity.Linear = (velocity.Linear + _gravityWideDt) * _linearDampingDt;
        velocity.Angular = velocity.Angular * _angularDampingDt;
    }
}
