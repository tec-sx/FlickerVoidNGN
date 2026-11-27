using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;

using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// The box or shape a greybox draws, made rather than loaded: a mesh name naming its size, or holding its shaped solid, which <see cref="MeshSources"/> turns into a grey
/// box standing on its origin, its texture coordinates in metres on every face.
/// </summary>
public static class GreyboxMesh
{
    private const string Prefix = "$greybox:";
    private const string SolidPrefix = "$solid:";

    private static readonly ConditionalWeakTable<SolidMesh, string> SolidNames = [];

    /// <summary>The mesh a greybox is drawn and picked as: the swap shown in its place, else its shape, else the box of its size.</summary>
    public static string GetName(in Greybox greybox)
    {
        if (greybox.GetShownSwap() is { } swap)
        {
            return swap;
        }

        if (greybox.Mesh is { } solid)
        {
            return GetName(solid);
        }

        return GetName(greybox.Size);
    }

    /// <summary>The mesh name of a solid: its points and faces written out, so the name alone makes it; made once for each solid.</summary>
    public static string GetName(SolidMesh solid)
    {
        return SolidNames.GetValue(solid, static solid => SolidPrefix + JsonSerializer.Serialize(solid, RuntimeJsonContext.Default.SolidMesh));
    }

    /// <summary>Reads the solid a mesh name holds; false for any other mesh, or a name that holds no closed solid.</summary>
    public static bool TryGetSolid(string mesh, [NotNullWhen(true)] out SolidMesh? solid)
    {
        solid = null;
        if (!mesh.StartsWith(SolidPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            solid = JsonSerializer.Deserialize(mesh.AsSpan(SolidPrefix.Length), RuntimeJsonContext.Default.SolidMesh);
        }
        catch (JsonException)
        {
            return false;
        }

        return solid is not null;
    }

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
