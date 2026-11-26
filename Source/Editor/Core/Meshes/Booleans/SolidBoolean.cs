using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes.Booleans;

/// <summary>
/// Combines two solids with Manifold: the sum, the difference or the part both share. The faces go through as triangles, and the
/// result's triangles that lie in one plane and make a convex quad are joined again, so a box cut by a box keeps its quads.
/// </summary>
public static class SolidBoolean
{
    private const float PlaneTolerance = 1e-4f;

    /// <summary>Whether Manifold's library was found, built by <c>Source/Native/Manifold</c>.</summary>
    public static bool IsAvailable
    {
        get
        {
            ManifoldNative.Register();
            try
            {
                nuint size = ManifoldNative.manifold_manifold_size();
                return size > 0;
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The first solid combined with the second, both in the same space; null when nothing is left, or Manifold finds a solid
    /// it can't work with (one that overlaps itself).
    /// </summary>
    public static SolidMesh? Combine(SolidMesh first, SolidMesh second, BooleanOperation operation)
    {
        ManifoldNative.Register();
        nint a = MakeManifold(first);
        nint b = MakeManifold(second);
        nint result = 0;
        try
        {
            if (a == 0 || b == 0)
            {
                return null;
            }

            result = ManifoldNative.manifold_alloc_manifold();
            ManifoldNative.manifold_boolean(result, a, b, (int)operation);
            return ReadSolid(result);
        }
        finally
        {
            Delete(a);
            Delete(b);
            Delete(result);
        }
    }

    private static void Delete(nint manifold)
    {
        if (manifold != 0)
        {
            ManifoldNative.manifold_delete_manifold(manifold);
        }
    }

    private static nint MakeManifold(SolidMesh solid)
    {
        float[] properties = new float[solid.Points.Count * 3];
        for (int point = 0; point < solid.Points.Count; point++)
        {
            Vector3 position = solid.Points[point];
            properties[point * 3] = position.X;
            properties[(point * 3) + 1] = position.Y;
            properties[(point * 3) + 2] = position.Z;
        }

        List<uint> triangles = [];
        foreach (IReadOnlyList<int> face in solid.Faces)
        {
            for (int corner = 1; corner + 1 < face.Count; corner++)
            {
                triangles.Add((uint)face[0]);
                triangles.Add((uint)face[corner]);
                triangles.Add((uint)face[corner + 1]);
            }
        }

        nint meshMemory = ManifoldNative.manifold_alloc_meshgl();
        nint mesh = ManifoldNative.manifold_meshgl(meshMemory, properties, (nuint)solid.Points.Count, 3, [.. triangles], (nuint)(triangles.Count / 3));
        nint manifoldMemory = ManifoldNative.manifold_alloc_manifold();
        nint manifold = ManifoldNative.manifold_of_meshgl(manifoldMemory, mesh);
        ManifoldNative.manifold_delete_meshgl(mesh);
        if (ManifoldNative.manifold_status(manifold) != 0)
        {
            ManifoldNative.manifold_delete_manifold(manifold);
            return 0;
        }

        return manifold;
    }

    private static SolidMesh? ReadSolid(nint manifold)
    {
        if (ManifoldNative.manifold_status(manifold) != 0)
        {
            return null;
        }

        nint meshMemory = ManifoldNative.manifold_alloc_meshgl();
        nint mesh = ManifoldNative.manifold_get_meshgl(meshMemory, manifold);
        try
        {
            int stride = (int)ManifoldNative.manifold_meshgl_num_prop(mesh);
            int propertyCount = (int)ManifoldNative.manifold_meshgl_vert_properties_length(mesh);
            int triangleCount = (int)ManifoldNative.manifold_meshgl_tri_length(mesh);
            if (triangleCount == 0 || stride < 3)
            {
                return null;
            }

            float[] properties = new float[propertyCount];
            uint[] triangles = new uint[triangleCount];
            CopyOut(properties, mem => ManifoldNative.manifold_meshgl_vert_properties(mem, mesh), propertyCount * sizeof(float));
            CopyOut(triangles, mem => ManifoldNative.manifold_meshgl_tri_verts(mem, mesh), triangleCount * sizeof(uint));
            List<Vector3> points = [];
            for (int index = 0; index + stride <= propertyCount; index += stride)
            {
                points.Add(new Vector3(properties[index], properties[index + 1], properties[index + 2]));
            }

            return Join(points, triangles);
        }
        finally
        {
            ManifoldNative.manifold_delete_meshgl(mesh);
        }
    }

    private static void CopyOut<T>(T[] target, Func<nint, nint> fill, int bytes)
        where T : unmanaged
    {
        nint buffer = Marshal.AllocHGlobal(bytes);
        try
        {
            fill(buffer);
            ReadOnlySpan<byte> raw;
            unsafe
            {
                raw = new ReadOnlySpan<byte>((void*)buffer, bytes);
            }

            MemoryMarshal.Cast<byte, T>(raw).CopyTo(target);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // Triangles that share an edge, lie in one plane and make a convex quad become one face.
    private static SolidMesh? Join(List<Vector3> points, uint[] triangles)
    {
        List<List<int>> faces = [];
        for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
        {
            faces.Add([(int)triangles[triangle], (int)triangles[triangle + 1], (int)triangles[triangle + 2]]);
        }

        bool joined = true;
        while (joined)
        {
            joined = TryJoinOne(points, faces);
        }

        List<IReadOnlyList<int>> result = [.. faces];
        try
        {
            return new SolidMesh(points, result);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool TryJoinOne(List<Vector3> points, List<List<int>> faces)
    {
        Dictionary<(int, int), int> owner = [];
        for (int face = 0; face < faces.Count; face++)
        {
            List<int> corners = faces[face];
            for (int corner = 0; corner < corners.Count; corner++)
            {
                owner[(corners[corner], corners[(corner + 1) % corners.Count])] = face;
            }
        }

        for (int face = 0; face < faces.Count; face++)
        {
            List<int> corners = faces[face];
            if (corners.Count != 3)
            {
                continue;
            }

            for (int corner = 0; corner < 3; corner++)
            {
                int from = corners[corner];
                int to = corners[(corner + 1) % 3];
                if (!owner.TryGetValue((to, from), out int other) || other == face || faces[other].Count != 3)
                {
                    continue;
                }

                List<int>? quad = TryMakeQuad(points, corners, corner, faces[other]);
                if (quad is null)
                {
                    continue;
                }

                faces[Math.Min(face, other)] = quad;
                faces.RemoveAt(Math.Max(face, other));
                return true;
            }
        }

        return false;
    }

    // The triangle's edge from corner to the next is shared with the other triangle, which runs it the other way.
    private static List<int>? TryMakeQuad(List<Vector3> points, List<int> triangle, int corner, List<int> other)
    {
        int from = triangle[corner];
        int to = triangle[(corner + 1) % 3];
        int apex = triangle[(corner + 2) % 3];
        int at = other.IndexOf(to);
        int across = other[(at + 2) % 3];
        List<int> quad = [from, across, to, apex];
        Vector3 normal = GetNormal(points, triangle);
        float depth = MathF.Abs(Vector3.Dot(points[across] - points[from], normal));
        if (depth > PlaneTolerance)
        {
            return null;
        }

        for (int index = 0; index < 4; index++)
        {
            Vector3 current = points[quad[index]];
            Vector3 next = points[quad[(index + 1) % 4]];
            Vector3 after = points[quad[(index + 2) % 4]];
            if (Vector3.Dot(Vector3.Cross(next - current, after - next), normal) <= 0)
            {
                return null;
            }
        }

        return quad;
    }

    private static Vector3 GetNormal(List<Vector3> points, List<int> triangle)
    {
        Vector3 first = points[triangle[0]];
        Vector3 cross = Vector3.Cross(points[triangle[1]] - first, points[triangle[2]] - first);
        return Vector3.Normalize(cross);
    }
}
