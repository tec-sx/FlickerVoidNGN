using System.Numerics;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>The smallest convex shape holding <see cref="Points"/>, in the entity's space: the collision of a moving part.</summary>
public sealed class HullCollider : Collider
{
    /// <summary>The radius Jolt rounds a hull's edges by for collision, at most; rays still meet its flat faces.</summary>
    private const float ConvexRadius = 0.05f;

    /// <summary>How far, in metres, points must stand off a line or plane to span a volume.</summary>
    private const float Tolerance = 1e-4f;

    public Vector3[] Points { get; set; } = [];

    internal override Jolt.Shape CreateShape()
    {
        // Jolt stops the process on points that span no volume, rather than failing, so they are refused first.
        if (!SpansVolume())
        {
            throw new InvalidOperationException("a hull needs points that span a volume, not a line or a plane.");
        }

        using Jolt.ConvexHullShapeSettings settings = new(Points, ConvexRadius);
        Jolt.Shape? shape = settings.Create();
        return shape ?? throw new InvalidOperationException("Jolt could not make a hull of its points.");
    }

    /// <summary>Whether some four points stand off each other's point, line and plane: the first, the farthest from it, and so on.</summary>
    private bool SpansVolume()
    {
        if (Points.Length < 4)
        {
            return false;
        }

        Vector3 first = Points[0];
        Vector3 second = FindFarthest(point => Vector3.Distance(point, first));
        Vector3 along = Vector3.Normalize(second - first);
        Vector3 third = FindFarthest(point => GetDistanceToLine(point, first, along));
        Vector3 across = Vector3.Cross(second - first, third - first);
        if (Vector3.Distance(first, second) < Tolerance || across.Length() < Tolerance * Tolerance)
        {
            return false;
        }

        Vector3 normal = Vector3.Normalize(across);
        Vector3 fourth = FindFarthest(point => GetDistanceToPlane(point, first, normal));
        float height = GetDistanceToPlane(fourth, first, normal);
        return height >= Tolerance;
    }

    private static float GetDistanceToLine(Vector3 point, Vector3 origin, Vector3 direction)
    {
        Vector3 cross = Vector3.Cross(point - origin, direction);
        return cross.Length();
    }

    private static float GetDistanceToPlane(Vector3 point, Vector3 origin, Vector3 normal)
    {
        float height = Vector3.Dot(point - origin, normal);
        return MathF.Abs(height);
    }

    private Vector3 FindFarthest(Func<Vector3, float> distance)
    {
        Vector3 farthest = Points[0];
        float farthestDistance = 0;
        foreach (Vector3 point in Points)
        {
            float pointDistance = distance(point);
            if (pointDistance > farthestDistance)
            {
                farthest = point;
                farthestDistance = pointDistance;
            }
        }

        return farthest;
    }
}
