using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Draws a solid while its mesh is edited: the edges and, in point mode, the points of the faces turned toward the camera (the
/// ones that can be picked), the faces and edges selected, and the element under the mouse.
/// </summary>
public static class MeshOverlay
{
    public static readonly Vector4 EdgeColor = new(0.85f, 0.8f, 0.7f, 1);
    public static readonly Vector4 PointColor = new(0.1f, 0.08f, 0.06f, 1);
    public static readonly Vector4 FaceFill = new(1, 0.55f, 0.1f, 0.3f);
    public static readonly Vector4 SelectedColor = EditorIcons.SelectedColor;
    public static readonly Vector4 HoverColor = new(1, 0.9f, 0.5f, 1);
    public static readonly Vector4 HoverFill = new(1, 0.9f, 0.5f, 0.25f);

    private const float EdgeWidth = OverlayLines.Thin;
    private const float SelectedWidth = OverlayLines.Bold;
    private const float PointHalf = 3;
    private const float SelectedPointHalf = 4;

    /// <summary>Adds the solid as placed by <paramref name="toWorld"/> to an overlay begun for the camera.</summary>
    public static void Draw(OverlayBuilder overlay, EditorCamera camera, SolidMesh solid, in Matrix4x4 toWorld, MeshSelection selection, MeshHit? hover)
    {
        SolidTopology topology = new(solid);
        bool[] facing = new bool[solid.Faces.Count];
        for (int face = 0; face < facing.Length; face++)
        {
            facing[face] = MeshPicker.IsFacingCamera(solid, toWorld, camera, face);
        }

        DrawSelectedFaces(overlay, solid, toWorld, selection, facing);
        foreach (SolidEdge edge in topology.Edges)
        {
            if (IsEdgeFacing(topology, edge, facing))
            {
                DrawEdge(overlay, solid, toWorld, edge, EdgeColor, EdgeWidth);
            }
        }

        foreach (SolidEdge edge in selection.Edges)
        {
            if (IsEdgeFacing(topology, edge, facing))
            {
                DrawEdge(overlay, solid, toWorld, edge, SelectedColor, SelectedWidth);
            }
        }

        if (selection.Element == MeshElement.Point)
        {
            DrawPoints(overlay, solid, toWorld, selection, facing);
        }

        if (hover is { } found)
        {
            DrawHover(overlay, solid, toWorld, found);
        }
    }

    private static bool IsEdgeFacing(SolidTopology topology, SolidEdge edge, bool[] facing)
    {
        (int along, int back) = topology.GetFaces(edge);
        return facing[along] || facing[back];
    }

    private static void DrawSelectedFaces(OverlayBuilder overlay, SolidMesh solid, in Matrix4x4 toWorld, MeshSelection selection, bool[] facing)
    {
        foreach (int face in selection.Faces)
        {
            if (facing[face])
            {
                DrawFace(overlay, solid, toWorld, face, FaceFill);
            }
        }
    }

    private static void DrawFace(OverlayBuilder overlay, SolidMesh solid, in Matrix4x4 toWorld, int face, Vector4 color)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 first = Vector3.Transform(solid.Points[corners[0]], toWorld);
        for (int corner = 1; corner + 1 < corners.Count; corner++)
        {
            Vector3 second = Vector3.Transform(solid.Points[corners[corner]], toWorld);
            Vector3 third = Vector3.Transform(solid.Points[corners[corner + 1]], toWorld);
            overlay.AddTriangle(first, second, third, color);
        }
    }

    private static void DrawEdge(OverlayBuilder overlay, SolidMesh solid, in Matrix4x4 toWorld, SolidEdge edge, Vector4 color, float width)
    {
        Vector3 from = Vector3.Transform(solid.Points[edge.A], toWorld);
        Vector3 to = Vector3.Transform(solid.Points[edge.B], toWorld);
        overlay.AddLine(from, to, color, width);
    }

    private static void DrawPoints(OverlayBuilder overlay, SolidMesh solid, in Matrix4x4 toWorld, MeshSelection selection, bool[] facing)
    {
        bool[] shown = new bool[solid.Points.Count];
        for (int face = 0; face < facing.Length; face++)
        {
            if (!facing[face])
            {
                continue;
            }

            foreach (int point in solid.Faces[face])
            {
                shown[point] = true;
            }
        }

        for (int point = 0; point < shown.Length; point++)
        {
            if (!shown[point])
            {
                continue;
            }

            Vector3 position = Vector3.Transform(solid.Points[point], toWorld);
            if (selection.Points.Contains(point))
            {
                overlay.AddCube(position, SelectedPointHalf, SelectedColor);
            }
            else
            {
                overlay.AddCube(position, PointHalf, PointColor);
            }
        }
    }

    private static void DrawHover(OverlayBuilder overlay, SolidMesh solid, in Matrix4x4 toWorld, MeshHit hit)
    {
        switch (hit.Element)
        {
            case MeshElement.Point:
                Vector3 position = Vector3.Transform(solid.Points[hit.Index], toWorld);
                overlay.AddCube(position, SelectedPointHalf, HoverColor);
                break;
            case MeshElement.Edge:
                DrawEdge(overlay, solid, toWorld, hit.Edge, HoverColor, SelectedWidth);
                break;
            default:
                DrawFace(overlay, solid, toWorld, hit.Index, HoverFill);
                break;
        }
    }
}
