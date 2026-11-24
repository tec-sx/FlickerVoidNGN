using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>The triangles paths are built over: the box around the collider of each body that is an obstacle, posed like the body.</summary>
public static class ObstacleGeometry
{
    private static readonly QueryDescription StaticBodies = new QueryDescription().WithAll<StaticBody, Transform>();
    private static readonly QueryDescription RigidBodies = new QueryDescription().WithAll<RigidBody, Transform>();

    /// <summary>The corners of a unit box, then its faces as triangles wound outward.</summary>
    private static readonly Vector3[] Corners =
    [
        new(-1, -1, -1), new(1, -1, -1), new(1, -1, 1), new(-1, -1, 1),
        new(-1, 1, -1), new(1, 1, -1), new(1, 1, 1), new(-1, 1, 1),
    ];

    private static readonly int[] Faces =
    [
        0, 1, 2, 0, 2, 3,
        4, 6, 5, 4, 7, 6,
        0, 4, 5, 0, 5, 1,
        1, 5, 6, 1, 6, 2,
        2, 6, 7, 2, 7, 3,
        3, 7, 4, 3, 4, 0,
    ];

    /// <summary>Adds the triangles of every obstacle, and calls <paramref name="found"/> with each one's entity and bounds.</summary>
    public static void Collect(World world, List<float> vertices, List<int> triangles, Action<Entity, Bounds>? found = null)
    {
        world.Query(in StaticBodies, (Entity entity, ref StaticBody body, ref Transform transform) =>
        {
            if (body.Obstacle)
            {
                Add(entity, body.Collider, transform, vertices, triangles, found);
            }
        });
        world.Query(in RigidBodies, (Entity entity, ref RigidBody body, ref Transform transform) =>
        {
            if (body.Obstacle)
            {
                Add(entity, body.Collider, transform, vertices, triangles, found);
            }
        });
    }

    /// <summary>The obstacle's collider and whether the entity has a body that is an obstacle.</summary>
    public static bool TryGetObstacle(World world, Entity entity, out Collider collider)
    {
        collider = null!;
        if (world.TryGet(entity, out StaticBody staticBody) && staticBody.Obstacle)
        {
            collider = staticBody.Collider;
            return true;
        }

        if (world.TryGet(entity, out RigidBody rigidBody) && rigidBody.Obstacle)
        {
            collider = rigidBody.Collider;
            return true;
        }

        return false;
    }

    /// <summary>The axis-aligned box around a collider's box posed by the transform.</summary>
    public static Bounds GetBounds(Collider collider, in Transform transform)
    {
        Vector3 halfSize = GetHalfSize(collider);
        Vector3 centre = transform.Position + Vector3.Transform(collider.Offset, transform.Rotation);
        Bounds box = new(-halfSize, halfSize);
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(transform.Rotation);
        Matrix4x4 placed = rotation * Matrix4x4.CreateTranslation(centre);
        return box.Transform(placed);
    }

    private static void Add(Entity entity, Collider collider, in Transform transform, List<float> vertices, List<int> triangles, Action<Entity, Bounds>? found)
    {
        Vector3 halfSize = GetHalfSize(collider);
        Vector3 centre = transform.Position + Vector3.Transform(collider.Offset, transform.Rotation);
        AddBox(centre, transform.Rotation, halfSize, vertices, triangles);
        if (found is not null)
        {
            Bounds bounds = GetBounds(collider, transform);
            found(entity, bounds);
        }
    }

    /// <summary>Half the size of the box around a collider in its own space; capsules stand along their Y axis.</summary>
    private static Vector3 GetHalfSize(Collider collider)
    {
        switch (collider)
        {
            case BoxCollider box:
                return box.Size / 2;
            case SphereCollider sphere:
                return new Vector3(sphere.Radius);
            case CapsuleCollider capsule:
                float halfHeight = (capsule.Length / 2) + capsule.Radius;
                return new Vector3(capsule.Radius, halfHeight, capsule.Radius);
            default:
                return Vector3.Zero;
        }
    }

    private static void AddBox(Vector3 centre, Quaternion rotation, Vector3 halfSize, List<float> vertices, List<int> triangles)
    {
        int first = vertices.Count / 3;
        foreach (Vector3 corner in Corners)
        {
            Vector3 local = corner * halfSize;
            Vector3 point = centre + Vector3.Transform(local, rotation);
            vertices.Add(point.X);
            vertices.Add(point.Y);
            vertices.Add(point.Z);
        }

        foreach (int face in Faces)
        {
            triangles.Add(first + face);
        }
    }
}
