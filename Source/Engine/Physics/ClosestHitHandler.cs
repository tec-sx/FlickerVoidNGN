using System.Numerics;

using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;

namespace FlickerVoid.Engine.Physics;

/// <summary>Keeps the nearest body a ray meets, passing through the one it ignores.</summary>
internal struct ClosestHitHandler : IRayHitHandler
{
    public bool Ignores;
    public CollidableReference Ignored;
    public bool Found;
    public float Distance;
    public Vector3 Normal;
    public CollidableReference Collidable;

    public readonly bool AllowTest(CollidableReference collidable) => !Ignores || collidable.Packed != Ignored.Packed;

    public readonly bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

    public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, CollidableReference collidable, int childIndex)
    {
        if (t >= maximumT)
        {
            return;
        }

        maximumT = t;
        Found = true;
        Distance = t;
        Normal = normal;
        Collidable = collidable;
    }
}
