using System.Numerics;

namespace FlickerVoid.Engine.Editor.Geometry;

/// <summary>A half-line from an origin along a unit direction, and where it meets the shapes the editor picks and drags with.</summary>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    private const float Epsilon = 1e-7f;

    public Vector3 GetPoint(float distance) => Origin + (Direction * distance);

    /// <summary>The ray in the space a matrix maps world space to; distances along it stay comparable only without scale.</summary>
    public Ray Transform(in Matrix4x4 matrix)
    {
        Vector3 origin = Vector3.Transform(Origin, matrix);
        Vector3 direction = Vector3.TransformNormal(Direction, matrix);
        return new Ray(origin, direction);
    }

    /// <summary>Möller–Trumbore: whether the ray passes through the triangle from either side, and how far along.</summary>
    public bool IntersectTriangle(Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = Vector3.Cross(Direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        if (MathF.Abs(determinant) < Epsilon)
        {
            return false;
        }

        float inverse = 1 / determinant;
        Vector3 toOrigin = Origin - a;
        float u = Vector3.Dot(toOrigin, p) * inverse;
        if (u < 0 || u > 1)
        {
            return false;
        }

        Vector3 q = Vector3.Cross(toOrigin, edge1);
        float v = Vector3.Dot(Direction, q) * inverse;
        if (v < 0 || u + v > 1)
        {
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverse;
        return distance >= 0;
    }

    /// <summary>Slab test against an axis-aligned box; a ray starting inside hits it at distance zero.</summary>
    public bool IntersectBox(Vector3 min, Vector3 max, out float distance)
    {
        Vector3 inverse = Vector3.One / Direction;
        Vector3 toMin = (min - Origin) * inverse;
        Vector3 toMax = (max - Origin) * inverse;
        Vector3 near = Vector3.Min(toMin, toMax);
        Vector3 far = Vector3.Max(toMin, toMax);
        float enter = MathF.Max(MathF.Max(near.X, near.Y), near.Z);
        float exit = MathF.Min(MathF.Min(far.X, far.Y), far.Z);
        distance = MathF.Max(enter, 0);
        return exit >= distance;
    }

    public bool IntersectSphere(Vector3 centre, float radius, out float distance)
    {
        distance = 0;
        Vector3 toCentre = centre - Origin;
        float along = Vector3.Dot(toCentre, Direction);
        float apartSquared = toCentre.LengthSquared() - (along * along);
        float radiusSquared = radius * radius;
        if (apartSquared > radiusSquared)
        {
            return false;
        }

        float halfChord = MathF.Sqrt(radiusSquared - apartSquared);
        float enter = along - halfChord;
        float exit = along + halfChord;
        if (exit < 0)
        {
            return false;
        }

        distance = MathF.Max(enter, 0);
        return true;
    }

    /// <summary>Where the ray meets a plane through a point, in front of its origin.</summary>
    public bool IntersectPlane(Vector3 point, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        float facing = Vector3.Dot(Direction, normal);
        if (MathF.Abs(facing) < Epsilon)
        {
            return false;
        }

        Vector3 toPoint = point - Origin;
        float distance = Vector3.Dot(toPoint, normal) / facing;
        if (distance < 0)
        {
            return false;
        }

        hit = GetPoint(distance);
        return true;
    }

    /// <summary>How far along a line (a point and a unit direction) its point nearest the ray's line lies; false when the two run parallel.</summary>
    public bool ClosestOnLine(Vector3 point, Vector3 direction, out float along)
    {
        along = 0;
        Vector3 between = point - Origin;
        float lineDotRay = Vector3.Dot(direction, Direction);
        float rayDotRay = Vector3.Dot(Direction, Direction);
        float lineDotLine = Vector3.Dot(direction, direction);
        float denominator = (lineDotLine * rayDotRay) - (lineDotRay * lineDotRay);
        if (MathF.Abs(denominator) < 1e-6f)
        {
            return false;
        }

        float lineDotBetween = Vector3.Dot(direction, between);
        float rayDotBetween = Vector3.Dot(Direction, between);
        along = ((lineDotRay * rayDotBetween) - (rayDotRay * lineDotBetween)) / denominator;
        return true;
    }
}
