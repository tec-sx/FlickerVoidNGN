using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Finds the point, edge or face of a solid under the mouse while its mesh is edited, on the CPU like <c>ScenePicker</c>: a face by
/// the ray through the mouse, an edge or a point by its distance on screen. Only what faces the camera can be picked: an edge or
/// point must touch a face turned toward it, and nothing hides it behind another face of the solid.
/// </summary>
public sealed class MeshPicker
{
    /// <summary>How near the mouse must be to an edge or a point on screen, in pixels, to pick it.</summary>
    public const float Reach = 10;

    private readonly SolidMesh _solid;
    private readonly Matrix4x4 _toWorld;
    private readonly EditorCamera _camera;
    private readonly int _width;
    private readonly int _height;
    private readonly SolidTopology _topology;
    private readonly bool[] _facing;

    /// <summary>Picks in the solid as placed by <paramref name="toWorld"/>, seen by the camera in a viewport of the given size in pixels.</summary>
    public MeshPicker(SolidMesh solid, Matrix4x4 toWorld, EditorCamera camera, int width, int height)
    {
        _solid = solid;
        _toWorld = toWorld;
        _camera = camera;
        _width = width;
        _height = height;
        _topology = new SolidTopology(solid);
        _facing = new bool[solid.Faces.Count];
        for (int face = 0; face < _facing.Length; face++)
        {
            _facing[face] = IsFacingCamera(solid, toWorld, camera, face);
        }
    }

    /// <summary>Whether the face's outside turns toward the camera.</summary>
    public static bool IsFacingCamera(SolidMesh solid, in Matrix4x4 toWorld, EditorCamera camera, int face)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 first = solid.Points[corners[0]];
        Vector3 normal = Vector3.Zero;
        Vector3 centre = Vector3.Zero;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            Vector3 point = solid.Points[corners[corner]];
            centre += point;
            if (corner >= 1 && corner + 1 < corners.Count)
            {
                Vector3 next = solid.Points[corners[corner + 1]];
                normal += Vector3.Cross(point - first, next - first);
            }
        }

        centre /= corners.Count;
        Vector3 worldNormal = Vector3.TransformNormal(normal, toWorld);
        Vector3 worldCentre = Vector3.Transform(centre, toWorld);
        Vector3 toEye = camera.GetDirectionToEye(worldCentre);
        float facing = Vector3.Dot(worldNormal, toEye);
        if (toWorld.GetDeterminant() < 0)
        {
            facing = -facing;
        }

        return facing > 0;
    }

    /// <summary>The element of the kind under the pixel, from the viewport's top left, or null.</summary>
    public MeshHit? Pick(MeshElement element, Vector2 pixel)
    {
        switch (element)
        {
            case MeshElement.Point:
                return PickPoint(pixel);
            case MeshElement.Edge:
                return PickEdge(pixel);
            default:
                return PickFace(pixel);
        }
    }

    private MeshHit? PickFace(Vector2 pixel)
    {
        Ray worldRay = _camera.GetRay(pixel, _width, _height);
        if (!Matrix4x4.Invert(_toWorld, out Matrix4x4 toLocal))
        {
            return null;
        }

        Ray ray = worldRay.Transform(toLocal);
        int nearest = -1;
        float nearestDistance = float.MaxValue;
        for (int face = 0; face < _solid.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = _solid.Faces[face];
            Vector3 first = _solid.Points[corners[0]];
            for (int corner = 1; corner + 1 < corners.Count; corner++)
            {
                Vector3 second = _solid.Points[corners[corner]];
                Vector3 third = _solid.Points[corners[corner + 1]];
                if (ray.IntersectTriangle(first, second, third, out float distance) && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = face;
                }
            }
        }

        if (nearest < 0)
        {
            return null;
        }

        SolidEdge edge = FindNearestEdge(nearest, pixel);
        return MeshHit.OfFace(nearest, edge);
    }

    private SolidEdge FindNearestEdge(int face, Vector2 pixel)
    {
        SolidEdge best = default;
        float bestDistance = float.MaxValue;
        foreach (SolidEdge edge in _topology.GetFaceEdges(face))
        {
            float distance = GetDistance(edge, pixel);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = edge;
            }
        }

        return best;
    }

    private MeshHit? PickEdge(Vector2 pixel)
    {
        SolidEdge? best = null;
        float bestDistance = Reach;
        foreach (SolidEdge edge in _topology.Edges)
        {
            if (!IsShown(edge))
            {
                continue;
            }

            float distance = GetDistance(edge, pixel);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = edge;
            }
        }

        if (best is not { } found)
        {
            return null;
        }

        return MeshHit.OfEdge(found);
    }

    private MeshHit? PickPoint(Vector2 pixel)
    {
        int best = -1;
        float bestDistance = Reach;
        for (int point = 0; point < _solid.Points.Count; point++)
        {
            if (!IsShown(point))
            {
                continue;
            }

            Vector3 world = Vector3.Transform(_solid.Points[point], _toWorld);
            if (!IsInFront(world))
            {
                continue;
            }

            Vector2 screen = _camera.Project(world, _width, _height);
            float distance = Vector2.Distance(screen, pixel);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        if (best < 0)
        {
            return null;
        }

        return MeshHit.OfPoint(best);
    }

    private bool IsShown(SolidEdge edge)
    {
        (int along, int back) = _topology.GetFaces(edge);
        return _facing[along] || _facing[back];
    }

    private bool IsShown(int point)
    {
        for (int face = 0; face < _facing.Length; face++)
        {
            if (_facing[face] && _solid.Faces[face].Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInFront(Vector3 world) => Vector3.Dot(world - _camera.Position, _camera.Forward) > 0.01f;

    /// <summary>The distance in pixels from the pixel to the edge as drawn, or far when it is behind the camera.</summary>
    private float GetDistance(SolidEdge edge, Vector2 pixel)
    {
        Vector3 from = Vector3.Transform(_solid.Points[edge.A], _toWorld);
        Vector3 to = Vector3.Transform(_solid.Points[edge.B], _toWorld);
        if (!IsInFront(from) || !IsInFront(to))
        {
            return float.MaxValue;
        }

        Vector2 start = _camera.Project(from, _width, _height);
        Vector2 end = _camera.Project(to, _width, _height);
        Vector2 along = end - start;
        float length = along.LengthSquared();
        float t = 0;
        if (length > 0)
        {
            t = Math.Clamp(Vector2.Dot(pixel - start, along) / length, 0, 1);
        }

        Vector2 closest = start + (along * t);
        return Vector2.Distance(closest, pixel);
    }
}
