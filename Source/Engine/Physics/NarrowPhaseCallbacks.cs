// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See THIRD-PARTY-NOTICES.md.
// Adapted from Stride.BepuPhysics' StrideNarrowPhaseCallbacks: one material with Stride's collidable defaults, no layers, groups or triggers yet.

using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace FlickerVoid.Engine.Physics;

/// <summary>Lets every pair with a dynamic body collide, with the same friction and contact springiness for all.</summary>
internal struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    private const float Friction = 1;
    private const float MaximumRecoveryVelocity = 1000;
    private const float SpringFrequency = 30;
    private const float SpringDampingRatio = 3;

    public void Initialize(Simulation simulation)
    {
    }

    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = Friction;
        pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
        pairMaterial.SpringSettings = new SpringSettings(SpringFrequency, SpringDampingRatio);
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        return true;
    }

    public void Dispose()
    {
    }
}
