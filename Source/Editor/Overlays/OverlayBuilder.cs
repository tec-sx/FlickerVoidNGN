using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Editor.Cameras;

namespace FlickerVoid.Engine.Editor.Overlays;

/// <summary>
/// Collects the coloured triangles the editor draws over the scene, such as gizmos and selection boxes. Sizes are in pixels and
/// turned into world units at each point, and lines face the camera, so shapes keep their look on screen as the camera moves.
/// </summary>
public sealed class OverlayBuilder
{
    private const int RoundSegments = 48;
    private const int ConeSegments = 12;

    private readonly List<OverlayVertex> _vertices = [];
    private readonly List<OverlayIcon> _icons = [];
    private EditorCamera _camera = new();
    private int _viewportHeight = 1;

    public ReadOnlySpan<OverlayVertex> Vertices => CollectionsMarshal.AsSpan(_vertices);

    /// <summary>Drawn over the triangles.</summary>
    public ReadOnlySpan<OverlayIcon> Icons => CollectionsMarshal.AsSpan(_icons);

    /// <summary>Empties the builder for a frame seen by the camera in a viewport of the given height.</summary>
    public void Begin(EditorCamera camera, int viewportHeight)
    {
        _vertices.Clear();
        _icons.Clear();
        _camera = camera;
        _viewportHeight = viewportHeight;
    }

    public void AddIcon(Vector3 position, EditorIcon icon, Vector4 color)
    {
        _icons.Add(new OverlayIcon(position, icon, color));
    }

    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector4 color)
    {
        _vertices.Add(new OverlayVertex(a, color));
        _vertices.Add(new OverlayVertex(b, color));
        _vertices.Add(new OverlayVertex(c, color));
    }

    /// <summary>A line of the given width in pixels, as a strip facing the camera.</summary>
    public void AddLine(Vector3 from, Vector3 to, Vector4 color, float widthPixels)
    {
        Vector3 along = to - from;
        if (along.LengthSquared() < 1e-12f)
        {
            return;
        }

        Vector3 middle = (from + to) / 2;
        Vector3 toEye = _camera.GetDirectionToEye(middle);
        Vector3 side = Vector3.Cross(along, toEye);
        if (side.LengthSquared() < 1e-12f)
        {
            return;
        }

        Vector3 unitSide = Vector3.Normalize(side);
        float fromHalf = _camera.WorldUnitsPerPixel(from, _viewportHeight) * widthPixels / 2;
        float toHalf = _camera.WorldUnitsPerPixel(to, _viewportHeight) * widthPixels / 2;
        Vector3 a = from - (unitSide * fromHalf);
        Vector3 b = from + (unitSide * fromHalf);
        Vector3 c = to + (unitSide * toHalf);
        Vector3 d = to - (unitSide * toHalf);
        AddTriangle(a, b, c, color);
        AddTriangle(a, c, d, color);
    }

    /// <summary>A circle round a centre in the plane across an axis, drawn as lines.</summary>
    public void AddCircle(Vector3 centre, Vector3 axis, float radius, Vector4 color, float widthPixels)
    {
        GetPlaneAxes(axis, out Vector3 first, out Vector3 second);
        Vector3 previous = centre + (first * radius);
        for (int segment = 1; segment <= RoundSegments; segment++)
        {
            float angle = MathF.Tau * segment / RoundSegments;
            Vector3 offset = (first * MathF.Cos(angle)) + (second * MathF.Sin(angle));
            Vector3 point = centre + (offset * radius);
            AddLine(previous, point, color, widthPixels);
            previous = point;
        }
    }

    /// <summary>A solid cone from a circular base to its tip, with the base radius in pixels.</summary>
    public void AddCone(Vector3 baseCentre, Vector3 tip, float radiusPixels, Vector4 color)
    {
        Vector3 axis = Vector3.Normalize(tip - baseCentre);
        GetPlaneAxes(axis, out Vector3 first, out Vector3 second);
        float radius = _camera.WorldUnitsPerPixel(baseCentre, _viewportHeight) * radiusPixels;
        Vector3 previous = baseCentre + (first * radius);
        for (int segment = 1; segment <= ConeSegments; segment++)
        {
            float angle = MathF.Tau * segment / ConeSegments;
            Vector3 offset = (first * MathF.Cos(angle)) + (second * MathF.Sin(angle));
            Vector3 point = baseCentre + (offset * radius);
            AddTriangle(previous, point, tip, color);
            AddTriangle(previous, baseCentre, point, color);
            previous = point;
        }
    }

    /// <summary>A solid axis-aligned cube round a centre, with its half size in pixels.</summary>
    public void AddCube(Vector3 centre, float halfPixels, Vector4 color)
    {
        float half = _camera.WorldUnitsPerPixel(centre, _viewportHeight) * halfPixels;
        Vector3 min = centre - new Vector3(half);
        Vector3 max = centre + new Vector3(half);
        Vector3[] corners = GetCorners(min, max, Matrix4x4.Identity);
        int[] faces = [0, 1, 3, 2, 4, 6, 7, 5, 0, 4, 5, 1, 2, 3, 7, 6, 0, 2, 6, 4, 1, 5, 7, 3];
        for (int face = 0; face < faces.Length; face += 4)
        {
            Vector3 a = corners[faces[face]];
            Vector3 b = corners[faces[face + 1]];
            Vector3 c = corners[faces[face + 2]];
            Vector3 d = corners[faces[face + 3]];
            AddTriangle(a, b, c, color);
            AddTriangle(a, c, d, color);
        }
    }

    /// <summary>The twelve edges of a box given in the space a matrix carries into the world.</summary>
    public void AddBox(Vector3 min, Vector3 max, in Matrix4x4 world, Vector4 color, float widthPixels)
    {
        Vector3[] corners = GetCorners(min, max, world);
        int[] edges = [0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7];
        for (int edge = 0; edge < edges.Length; edge += 2)
        {
            Vector3 from = corners[edges[edge]];
            Vector3 to = corners[edges[edge + 1]];
            AddLine(from, to, color, widthPixels);
        }
    }

    /// <summary>Two unit axes across the given one, square to each other.</summary>
    public static void GetPlaneAxes(Vector3 axis, out Vector3 first, out Vector3 second)
    {
        Vector3 helper = Vector3.UnitY;
        if (MathF.Abs(Vector3.Dot(axis, helper)) > 0.9f)
        {
            helper = Vector3.UnitX;
        }

        Vector3 across = Vector3.Cross(axis, helper);
        first = Vector3.Normalize(across);
        second = Vector3.Cross(axis, first);
    }

    // Corner i takes x from bit 0, y from bit 1 and z from bit 2 of i, the minimum when the bit is clear.
    private static Vector3[] GetCorners(Vector3 min, Vector3 max, in Matrix4x4 world)
    {
        Vector3[] corners = new Vector3[8];
        for (int corner = 0; corner < 8; corner++)
        {
            float x = (corner & 1) == 0 ? min.X : max.X;
            float y = (corner & 2) == 0 ? min.Y : max.Y;
            float z = (corner & 4) == 0 ? min.Z : max.Z;
            Vector3 local = new(x, y, z);
            corners[corner] = Vector3.Transform(local, world);
        }

        return corners;
    }
}
