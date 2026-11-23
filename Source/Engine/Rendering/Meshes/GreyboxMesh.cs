using System.Globalization;
using System.Numerics;

using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// The box a greybox draws, made rather than loaded: a mesh name naming its size, which the mesh library and the editor's
/// picking turn into a grey box standing on its origin, its texture coordinates in metres on every face.
/// </summary>
public static class GreyboxMesh
{
    private const string Prefix = "$greybox:";

    public static readonly Vector4 Color = new(0.6f, 0.6f, 0.6f, 1);

    /// <summary>Each face's outward normal and the two directions across it, so the corners below go round it counter-clockwise seen from outside.</summary>
    private static readonly (Vector3 Normal, Vector3 Right, Vector3 Up)[] Faces =
    [
        (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY),
        (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ),
        (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY),
        (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY),
    ];

    /// <summary>The mesh name of a box of <paramref name="size"/>.</summary>
    public static string GetName(Vector3 size)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}{size.X:R},{size.Y:R},{size.Z:R}");
    }

    /// <summary>Reads the size a greybox mesh name names; false for any other mesh.</summary>
    public static bool TryGetSize(string mesh, out Vector3 size)
    {
        size = default;
        if (!mesh.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = mesh[Prefix.Length..].Split(',');
        if (parts.Length != 3)
        {
            return false;
        }

        bool x = float.TryParse(parts[0], CultureInfo.InvariantCulture, out float width);
        bool y = float.TryParse(parts[1], CultureInfo.InvariantCulture, out float height);
        bool z = float.TryParse(parts[2], CultureInfo.InvariantCulture, out float length);
        size = new Vector3(width, height, length);
        return x && y && z;
    }

    /// <summary>A box of <paramref name="size"/> standing on the origin, centred across, wound clockwise seen from outside as the engine draws.</summary>
    public static ModelData Create(Vector3 size)
    {
        Vector3 half = size / 2;
        Vector3 centre = new(0, half.Y, 0);
        List<MeshVertex> vertices = [];
        List<uint> indices = [];
        foreach ((Vector3 normal, Vector3 right, Vector3 up) in Faces)
        {
            float width = Math.Abs(Vector3.Dot(size, right));
            float height = Math.Abs(Vector3.Dot(size, up));
            Vector3 middle = centre + (normal * Math.Abs(Vector3.Dot(half, normal)));
            Vector3 across = right * (width / 2);
            Vector3 along = up * (height / 2);
            uint first = (uint)vertices.Count;
            vertices.Add(new MeshVertex(middle - across - along, normal, new Vector2(0, height)));
            vertices.Add(new MeshVertex(middle + across - along, normal, new Vector2(width, height)));
            vertices.Add(new MeshVertex(middle + across + along, normal, new Vector2(width, 0)));
            vertices.Add(new MeshVertex(middle - across + along, normal, new Vector2(0, 0)));
            indices.AddRange([first, first + 2, first + 1, first, first + 3, first + 2]);
        }

        MaterialData material = new(Color, null);
        MeshPart part = new([.. vertices], [.. indices], material);
        return new ModelData([part], []);
    }
}
