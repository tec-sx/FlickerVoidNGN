using System.Globalization;
using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// The box a greybox draws, made rather than loaded: a mesh name naming its size, which <see cref="MeshSources"/> turns into a grey
/// box standing on its origin, its texture coordinates in metres on every face.
/// </summary>
public static class GreyboxMesh
{
    private const string Prefix = "$greybox:";

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

    /// <summary>A box of <paramref name="size"/> standing on the origin, centred across, drawn as solids are (<see cref="SolidModel"/>).</summary>
    public static ModelData Create(Vector3 size)
    {
        SolidMesh box = SolidMesh.Box(size);
        return SolidModel.Create(box);
    }
}
