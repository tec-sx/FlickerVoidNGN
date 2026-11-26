using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

/// <summary>Solids for the Boolean tests, and the check for Manifold's library, which the tests skip without unless CI says it must be there.</summary>
internal static class SolidBooleanTestsBuilder
{
    /// <summary>Skips the test where the library isn't built; fails it where it must be (the CI sets <c>FLICKERVOID_REQUIRE_MANIFOLD</c>).</summary>
    public static void RequireLibrary()
    {
        if (SolidBoolean.IsAvailable)
        {
            return;
        }

        string? required = Environment.GetEnvironmentVariable("FLICKERVOID_REQUIRE_MANIFOLD");
        if (required == "1")
        {
            throw new InvalidOperationException("Manifold's library was not found, and this run needs it.");
        }

        Assert.Skip("Manifold's library is not built: run Source/Native/Manifold/build.sh or build.ps1.");
    }

    /// <summary>A box standing on the point, centred across.</summary>
    public static SolidMesh BoxAt(Vector3 size, Vector3 at)
    {
        SolidMesh box = SolidMesh.Box(size);
        HashSet<int> all = [.. Enumerable.Range(0, box.Points.Count)];
        return MeshEdit.Move(box, all, at);
    }

    /// <summary>The volume of a closed solid, from the signed tetrahedra its triangles make with the origin.</summary>
    public static float GetVolume(SolidMesh solid)
    {
        float volume = 0;
        foreach (IReadOnlyList<int> face in solid.Faces)
        {
            Vector3 first = solid.Points[face[0]];
            for (int corner = 1; corner + 1 < face.Count; corner++)
            {
                Vector3 second = solid.Points[face[corner]];
                Vector3 third = solid.Points[face[corner + 1]];
                volume += Vector3.Dot(first, Vector3.Cross(second, third)) / 6;
            }
        }

        return volume;
    }
}
