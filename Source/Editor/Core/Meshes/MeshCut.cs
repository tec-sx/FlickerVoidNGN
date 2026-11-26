using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Cuts the selected faces into the solid: a pocket to a depth, or an opening through it. A pocket pushes the faces in, as a
/// negative extrude does. An opening subtracts a prism of each face, running through the solid, with Manifold; the faces must be
/// convex for it, and an opening smaller than its face wants an inset first.
/// </summary>
public static class MeshCut
{
    /// <summary>How far the prism stands out of the face it cuts, so the cut never leaves a sliver of the face.</summary>
    private const float Overshoot = 0.05f;

    /// <summary>
    /// The solid with the faces cut in to the depth, or through when <paramref name="through"/>; null when no face is selected, a
    /// pocket would take every face, or a through cut finds no Manifold library or leaves nothing.
    /// </summary>
    public static MeshResult? Faces(SolidMesh solid, IReadOnlySet<int> faces, float depth, bool through)
    {
        if (faces.Count == 0)
        {
            return null;
        }

        if (!through)
        {
            if (depth <= 0)
            {
                return null;
            }

            return MeshExtrude.Faces(solid, faces, -depth);
        }

        if (!SolidBoolean.IsAvailable)
        {
            return null;
        }

        float length = GetSize(solid) + Overshoot;
        SolidMesh? cut = solid;
        foreach (int face in faces)
        {
            SolidMesh prism = MakePrism(solid, face, length);
            cut = SolidBoolean.Combine(cut!, prism, BooleanOperation.Subtract);
            if (cut is null)
            {
                return null;
            }
        }

        return new MeshResult(cut!, []);
    }

    private static float GetSize(SolidMesh solid)
    {
        Vector3 low = new(float.MaxValue);
        Vector3 high = new(float.MinValue);
        foreach (Vector3 point in solid.Points)
        {
            low = Vector3.Min(low, point);
            high = Vector3.Max(high, point);
        }

        return Vector3.Distance(low, high);
    }

    // The face's outline pushed out by the overshoot and in by the length, closed by sides.
    private static SolidMesh MakePrism(SolidMesh solid, int face, float length)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 normal = GetNormal(solid, corners);
        int count = corners.Count;
        List<Vector3> points = [];
        foreach (int corner in corners)
        {
            points.Add(solid.Points[corner] + (normal * Overshoot));
        }

        foreach (int corner in corners)
        {
            points.Add(solid.Points[corner] - (normal * length));
        }

        List<IReadOnlyList<int>> faces = [];
        faces.Add([.. Enumerable.Range(0, count)]);
        faces.Add([.. Enumerable.Range(count, count).Reverse()]);
        for (int index = 0; index < count; index++)
        {
            int next = (index + 1) % count;
            faces.Add([index, count + index, count + next, next]);
        }

        return new SolidMesh(points, faces);
    }

    private static Vector3 GetNormal(SolidMesh solid, IReadOnlyList<int> corners)
    {
        Vector3 sum = Vector3.Zero;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            Vector3 current = solid.Points[corners[corner]];
            Vector3 next = solid.Points[corners[(corner + 1) % corners.Count]];
            sum += Vector3.Cross(current, next);
        }

        return Vector3.Normalize(sum);
    }
}
