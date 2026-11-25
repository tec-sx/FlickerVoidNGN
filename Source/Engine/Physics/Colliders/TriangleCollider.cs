using System.Numerics;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>
/// Triangles in the entity's space, three <see cref="Indices"/> into <see cref="Positions"/> each, wound as the engine's models
/// (clockwise seen from the front), which collide from their front: the exact collision of a standing thing, so only static bodies take it.
/// </summary>
public sealed class TriangleCollider : Collider
{
    public Vector3[] Positions { get; set; } = [];

    public int[] Indices { get; set; } = [];

    internal override Jolt.Shape CreateShape()
    {
        // Jolt's triangles face the side they turn counter-clockwise to, so each is listed the other way round.
        Jolt.IndexedTriangle[] triangles = new Jolt.IndexedTriangle[Indices.Length / 3];
        for (int index = 0; index < triangles.Length; index++)
        {
            int first = Indices[index * 3];
            int second = Indices[(index * 3) + 2];
            int third = Indices[(index * 3) + 1];
            triangles[index] = new Jolt.IndexedTriangle(in first, in second, in third, 0, 0);
        }

        using Jolt.MeshShapeSettings settings = new(Positions, triangles);
        Jolt.Shape? shape = settings.Create();
        return shape ?? throw new InvalidOperationException("its triangles make no shape.");
    }
}
