using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>The triangles paths are built over: the box around each static body's collider, posed like the body.</summary>
public static class StaticGeometry
{
    private static readonly QueryDescription StaticBodies = new QueryDescription().WithAll<StaticBody, Transform>();

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

    public static void Collect(World world, List<float> vertices, List<int> triangles)
    {
        world.Query(in StaticBodies, (ref StaticBody body, ref Transform transform) =>
        {
            Vector3 halfSize = GetHalfSize(body.Collider);
            Vector3 centre = transform.Position + Vector3.Transform(body.Collider.Offset, transform.Rotation);
            AddBox(centre, transform.Rotation, halfSize, vertices, triangles);
        });
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
