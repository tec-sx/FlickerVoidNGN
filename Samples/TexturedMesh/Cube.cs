using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>A unit cube with one quad per face, each face textured with the whole texture and wound clockwise seen from outside.</summary>
public static class Cube
{
    /// <summary>Each face's outward normal, then its right and up directions seen from outside.</summary>
    private static readonly (Vector3 Normal, Vector3 Right, Vector3 Up)[] Faces =
    [
        (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY),
        (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ),
        (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY),
        (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY),
    ];

    /// <summary>Corners of a face in clockwise order seen from outside: bottom left, top left, top right, bottom right.</summary>
    private static readonly Vector2[] Corners = [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)];

    /// <summary>Positions, normals and uvs, 32 bytes per vertex, as Content/Mesh.hlsl reads them.</summary>
    public static byte[] CreateVertices()
    {
        List<float> data = [];
        foreach ((Vector3 normal, Vector3 right, Vector3 up) in Faces)
        {
            foreach (Vector2 corner in Corners)
            {
                Vector3 position = (normal + corner.X * right + corner.Y * up) * 0.5f;
                Vector2 uv = new((corner.X + 1) * 0.5f, (1 - corner.Y) * 0.5f);
                data.AddRange([position.X, position.Y, position.Z, normal.X, normal.Y, normal.Z, uv.X, uv.Y]);
            }
        }

        float[] floats = [.. data];
        return MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
    }

    /// <summary>Two triangles per face, as 16-bit indices.</summary>
    public static ushort[] CreateIndices()
    {
        List<ushort> indices = [];
        for (int face = 0; face < Faces.Length; face++)
        {
            ushort first = (ushort)(face * Corners.Length);
            indices.AddRange([first, (ushort)(first + 1), (ushort)(first + 2), first, (ushort)(first + 2), (ushort)(first + 3)]);
        }

        return [.. indices];
    }
}
