using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>
/// The triangles paths are built over, posed like their bodies: the box around the collider of each body that is an obstacle, and
/// for a triangle collider its own triangles, so the floors, walls and openings of a standing thing are walked as they are.
/// </summary>
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
        Bounds box = GetLocalBounds(collider);
        Vector3 centre = transform.Position + Vector3.Transform(collider.Offset, transform.Rotation);
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(transform.Rotation);
        Matrix4x4 placed = rotation * Matrix4x4.CreateTranslation(centre);
        return box.Transform(placed);
    }

    private static void Add(Entity entity, Collider collider, in Transform transform, List<float> vertices, List<int> triangles, Action<Entity, Bounds>? found)
    {
        Vector3 centre = transform.Position + Vector3.Transform(collider.Offset, transform.Rotation);
        if (collider is TriangleCollider mesh)
        {
            AddTriangles(mesh, centre, transform.Rotation, vertices, triangles);
        }
        else
        {
            Bounds box = GetLocalBounds(collider);
            Vector3 boxCentre = centre + Vector3.Transform(box.Center, transform.Rotation);
            AddBox(boxCentre, transform.Rotation, box.Extents, vertices, triangles);
        }

        if (found is not null)
        {
            Bounds bounds = GetBounds(collider, transform);
            found(entity, bounds);
        }
    }

    /// <summary>The box around a collider in its own space, about its offset; capsules stand along their Y axis.</summary>
    private static Bounds GetLocalBounds(Collider collider)
    {
        switch (collider)
        {
            case BoxCollider box:
                return new Bounds(-box.Size / 2, box.Size / 2);
            case SphereCollider sphere:
                Vector3 radius = new(sphere.Radius);
                return new Bounds(-radius, radius);
            case CapsuleCollider capsule:
                float halfHeight = (capsule.Length / 2) + capsule.Radius;
                Vector3 half = new(capsule.Radius, halfHeight, capsule.Radius);
                return new Bounds(-half, half);
            case HullCollider hull:
                return GetBoundsOf(hull.Points);
            case TriangleCollider mesh:
                return GetBoundsOf(mesh.Positions);
            default:
                return new Bounds(Vector3.Zero, Vector3.Zero);
        }
    }

    private static Bounds GetBoundsOf(Vector3[] points)
    {
        if (points.Length == 0)
        {
            return new Bounds(Vector3.Zero, Vector3.Zero);
        }

        Bounds bounds = Bounds.Empty;
        foreach (Vector3 point in points)
        {
            bounds = bounds.Include(point);
        }

        return bounds;
    }

    /// <summary>The collider's own triangles; the engine winds them clockwise from the front, and paths want the other way.</summary>
    private static void AddTriangles(TriangleCollider mesh, Vector3 centre, Quaternion rotation, List<float> vertices, List<int> triangles)
    {
        int first = vertices.Count / 3;
        foreach (Vector3 position in mesh.Positions)
        {
            Vector3 point = centre + Vector3.Transform(position, rotation);
            vertices.Add(point.X);
            vertices.Add(point.Y);
            vertices.Add(point.Z);
        }

        for (int index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            triangles.Add(first + mesh.Indices[index]);
            triangles.Add(first + mesh.Indices[index + 2]);
            triangles.Add(first + mesh.Indices[index + 1]);
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
