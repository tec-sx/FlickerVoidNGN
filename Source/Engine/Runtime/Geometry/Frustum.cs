using System.Numerics;

namespace FlickerVoid.Engine.Runtime.Geometry;

/// <summary>The six planes around what a view-projection sees, facing in, to skip boxes wholly outside a view, such as meshes when drawing.</summary>
public readonly struct Frustum
{
    private readonly Vector4 _left;
    private readonly Vector4 _right;
    private readonly Vector4 _bottom;
    private readonly Vector4 _top;
    private readonly Vector4 _near;
    private readonly Vector4 _far;

    /// <summary>The frustum of a view-projection in System.Numerics' row-vector convention with depth from 0 to 1, as cameras and shadow views make.</summary>
    public Frustum(in Matrix4x4 viewProjection)
    {
        Vector4 x = new(viewProjection.M11, viewProjection.M21, viewProjection.M31, viewProjection.M41);
        Vector4 y = new(viewProjection.M12, viewProjection.M22, viewProjection.M32, viewProjection.M42);
        Vector4 z = new(viewProjection.M13, viewProjection.M23, viewProjection.M33, viewProjection.M43);
        Vector4 w = new(viewProjection.M14, viewProjection.M24, viewProjection.M34, viewProjection.M44);
        _left = w + x;
        _right = w - x;
        _bottom = w + y;
        _top = w - y;
        _near = z;
        _far = w - z;
    }

    /// <summary>Whether any of the box may be in view: false only when it lies wholly outside one of the planes, and for an empty box.</summary>
    public bool Intersects(in Bounds bounds)
    {
        if (bounds.IsEmpty)
        {
            return false;
        }

        return IsInside(_left, bounds) && IsInside(_right, bounds) && IsInside(_bottom, bounds) && IsInside(_top, bounds) && IsInside(_near, bounds) && IsInside(_far, bounds);
    }

    /// <summary>Whether the box's corner farthest along the plane's normal is on its inner side.</summary>
    private static bool IsInside(in Vector4 plane, in Bounds bounds)
    {
        float x = plane.X >= 0 ? bounds.Max.X : bounds.Min.X;
        float y = plane.Y >= 0 ? bounds.Max.Y : bounds.Min.Y;
        float z = plane.Z >= 0 ? bounds.Max.Z : bounds.Min.Z;
        return (plane.X * x) + (plane.Y * y) + (plane.Z * z) + plane.W >= 0;
    }
}
