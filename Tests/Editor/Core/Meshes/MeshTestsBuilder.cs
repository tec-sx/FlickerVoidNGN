using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

/// <summary>Makes the solids and the view the mesh editing tests work on.</summary>
internal static class MeshTestsBuilder
{
    public const int Width = 800;
    public const int Height = 600;

    public static SolidMesh Box() => SolidMesh.Box(Vector3.One);

    /// <summary>A bar of square rings along the up axis, closed by a cap at each end: point <c>ring * 4 + side</c>.</summary>
    public static SolidMesh Bar(int rings)
    {
        const int Sides = 4;
        Vector3[] points = new Vector3[rings * Sides];
        for (int ring = 0; ring < rings; ring++)
        {
            for (int side = 0; side < Sides; side++)
            {
                float angle = MathF.Tau * side / Sides;
                points[(ring * Sides) + side] = new Vector3(MathF.Cos(angle), ring, -MathF.Sin(angle));
            }
        }

        List<IReadOnlyList<int>> faces = [];
        for (int ring = 0; ring + 1 < rings; ring++)
        {
            for (int side = 0; side < Sides; side++)
            {
                int next = (side + 1) % Sides;
                faces.Add([(ring * Sides) + side, (ring * Sides) + next, ((ring + 1) * Sides) + next, ((ring + 1) * Sides) + side]);
            }
        }

        faces.Add([.. Enumerable.Range(0, Sides).Select(side => ((rings - 1) * Sides) + side)]);
        faces.Add([.. Enumerable.Range(0, Sides).Reverse()]);
        return new SolidMesh(points, faces);
    }

    /// <summary>A camera in front of the origin looking down the Z axis at the unit box's middle.</summary>
    public static EditorCamera FrontCamera()
    {
        EditorCamera camera = new();
        camera.LookAt(new Vector3(0, 0.5f, 0), 0, 0, 10);
        return camera;
    }

    /// <summary>A camera looking down at the box from its front, right and above, so three faces and seven corners show.</summary>
    public static EditorCamera CornerCamera()
    {
        EditorCamera camera = new();
        camera.LookAt(new Vector3(0, 0.5f, 0), 0.6f, -0.5f, 10);
        return camera;
    }
}
