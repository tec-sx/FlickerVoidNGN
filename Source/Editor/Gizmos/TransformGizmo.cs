using System.Numerics;

using FlickerVoid.Engine.Editor.Cameras;
using FlickerVoid.Engine.Editor.Geometry;
using FlickerVoid.Engine.Editor.Overlays;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Gizmos;

/// <summary>
/// The handles that move, rotate and scale a transform along the world axes: arrows, rings or cube-tipped lines that keep their
/// size on screen. Finds the handle under the mouse, works out the transform as a handle is dragged, and draws itself.
/// </summary>
public sealed class TransformGizmo
{
    /// <summary>How long the handles are on screen.</summary>
    public const float HandlePixels = 100;

    /// <summary>How far dragging the uniform scale handle right or up doubles the scale.</summary>
    public const float PixelsPerUniformScale = 100;

    private const float GrabPixels = 8;
    private const float CentrePixels = 10;
    private const int RingSamples = 48;
    private const float LineWidth = 3;
    private const float ArrowShaft = 0.8f;
    private const float ConeRadius = 6;
    private const float ScaleShaft = 0.9f;
    private const float TipCube = 5;
    private const float CentreCube = 7;
    private const float MinScale = 0.001f;

    private static readonly Vector4 XColor = new(0.9f, 0.15f, 0.15f, 1);
    private static readonly Vector4 YColor = new(0.3f, 0.85f, 0.2f, 1);
    private static readonly Vector4 ZColor = new(0.15f, 0.35f, 1, 1);
    private static readonly Vector4 CentreColor = new(0.9f, 0.9f, 0.9f, 1);
    private static readonly Vector4 HoverColor = new(1, 0.85f, 0.1f, 1);

    private Transform _start;
    private Vector2 _startPixel;
    private float _startAlong;
    private Vector3 _startOffset;
    private float _handleLength;

    public GizmoMode Mode { get; set; } = GizmoMode.Move;

    /// <summary>The handle under the mouse, or being dragged.</summary>
    public GizmoAxis Hovered { get; private set; }

    public bool IsDragging { get; private set; }

    public float GetHandleLength(EditorCamera camera, Vector3 centre, int viewportHeight)
    {
        return camera.WorldUnitsPerPixel(centre, viewportHeight) * HandlePixels;
    }

    /// <summary>Finds the handle under the mouse; while dragging, keeps the dragged one.</summary>
    public GizmoAxis Hover(EditorCamera camera, in Transform target, Vector2 pixel, int width, int height)
    {
        if (!IsDragging)
        {
            Hovered = FindAxis(camera, target.Position, pixel, width, height);
        }

        return Hovered;
    }

    /// <summary>Starts dragging the handle under the mouse; false when there is none.</summary>
    public bool BeginDrag(EditorCamera camera, in Transform target, Vector2 pixel, int width, int height)
    {
        GizmoAxis axis = FindAxis(camera, target.Position, pixel, width, height);
        if (axis == GizmoAxis.None)
        {
            return false;
        }

        Vector3 centre = target.Position;
        Ray ray = camera.GetRay(pixel, width, height);
        Vector3 direction = GetDirection(axis);
        switch (Mode)
        {
            case GizmoMode.Rotate:
                if (!ray.IntersectPlane(centre, direction, out Vector3 hit))
                {
                    return false;
                }

                _startOffset = hit - centre;
                break;
            default:
                if (axis != GizmoAxis.All && !ray.ClosestOnLine(centre, direction, out _startAlong))
                {
                    return false;
                }

                break;
        }

        Hovered = axis;
        IsDragging = true;
        _start = target;
        _startPixel = pixel;
        _handleLength = GetHandleLength(camera, centre, height);
        return true;
    }

    /// <summary>The transform the drag so far makes of the one it started from.</summary>
    public Transform Drag(EditorCamera camera, Vector2 pixel, int width, int height)
    {
        if (!IsDragging)
        {
            return _start;
        }

        Ray ray = camera.GetRay(pixel, width, height);
        switch (Mode)
        {
            case GizmoMode.Move:
                return DragMove(ray);
            case GizmoMode.Rotate:
                return DragRotate(ray);
            case GizmoMode.Scale:
                return DragScale(ray, pixel);
            default:
                return _start;
        }
    }

    public void EndDrag()
    {
        IsDragging = false;
    }

    public void Draw(OverlayBuilder overlay, EditorCamera camera, in Transform target, int viewportHeight)
    {
        if (Mode == GizmoMode.Select)
        {
            return;
        }

        Vector3 centre = target.Position;
        float length = GetHandleLength(camera, centre, viewportHeight);
        GizmoAxis[] axes = [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];
        foreach (GizmoAxis axis in axes)
        {
            Vector3 direction = GetDirection(axis);
            Vector4 color = GetColor(axis);
            Vector3 tip = centre + (direction * length);
            switch (Mode)
            {
                case GizmoMode.Move:
                    Vector3 shaftEnd = centre + (direction * (length * ArrowShaft));
                    overlay.AddLine(centre, shaftEnd, color, LineWidth);
                    overlay.AddCone(shaftEnd, tip, ConeRadius, color);
                    break;
                case GizmoMode.Rotate:
                    overlay.AddCircle(centre, direction, length, color, LineWidth);
                    break;
                case GizmoMode.Scale:
                    Vector3 lineEnd = centre + (direction * (length * ScaleShaft));
                    overlay.AddLine(centre, lineEnd, color, LineWidth);
                    overlay.AddCube(tip, TipCube, color);
                    break;
            }
        }

        if (Mode == GizmoMode.Scale)
        {
            Vector4 centreColor = GetColor(GizmoAxis.All);
            overlay.AddCube(centre, CentreCube, centreColor);
        }
    }

    private static Vector3 GetDirection(GizmoAxis axis)
    {
        switch (axis)
        {
            case GizmoAxis.X:
                return Vector3.UnitX;
            case GizmoAxis.Y:
                return Vector3.UnitY;
            case GizmoAxis.Z:
                return Vector3.UnitZ;
            default:
                return Vector3.Zero;
        }
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 along = to - from;
        float lengthSquared = along.LengthSquared();
        float t = 0;
        if (lengthSquared > 0)
        {
            float projected = Vector2.Dot(point - from, along) / lengthSquared;
            t = Math.Clamp(projected, 0, 1);
        }

        Vector2 nearest = from + (along * t);
        return Vector2.Distance(point, nearest);
    }

    private Vector4 GetColor(GizmoAxis axis)
    {
        if (axis == Hovered)
        {
            return HoverColor;
        }

        switch (axis)
        {
            case GizmoAxis.X:
                return XColor;
            case GizmoAxis.Y:
                return YColor;
            case GizmoAxis.Z:
                return ZColor;
            default:
                return CentreColor;
        }
    }

    private GizmoAxis FindAxis(EditorCamera camera, Vector3 centre, Vector2 pixel, int width, int height)
    {
        if (Mode == GizmoMode.Select)
        {
            return GizmoAxis.None;
        }

        Vector2 centrePixel = camera.Project(centre, width, height);
        if (Mode == GizmoMode.Scale && Vector2.Distance(pixel, centrePixel) < CentrePixels)
        {
            return GizmoAxis.All;
        }

        float length = GetHandleLength(camera, centre, height);
        GizmoAxis nearest = GizmoAxis.None;
        float nearestDistance = GrabPixels;
        GizmoAxis[] axes = [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];
        foreach (GizmoAxis axis in axes)
        {
            Vector3 direction = GetDirection(axis);
            float distance;
            if (Mode == GizmoMode.Rotate)
            {
                distance = DistanceToRing(camera, centre, direction, length, pixel, width, height);
            }
            else
            {
                Vector3 tip = centre + (direction * length);
                Vector2 tipPixel = camera.Project(tip, width, height);
                distance = DistanceToSegment(pixel, centrePixel, tipPixel);
            }

            if (distance < nearestDistance)
            {
                nearest = axis;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static float DistanceToRing(EditorCamera camera, Vector3 centre, Vector3 axis, float radius, Vector2 pixel, int width, int height)
    {
        OverlayBuilder.GetPlaneAxes(axis, out Vector3 first, out Vector3 second);
        float nearest = float.PositiveInfinity;
        Vector3 firstPoint = centre + (first * radius);
        Vector2 previous = camera.Project(firstPoint, width, height);
        for (int sample = 1; sample <= RingSamples; sample++)
        {
            float angle = MathF.Tau * sample / RingSamples;
            Vector3 offset = (first * MathF.Cos(angle)) + (second * MathF.Sin(angle));
            Vector3 point = centre + (offset * radius);
            Vector2 current = camera.Project(point, width, height);
            float distance = DistanceToSegment(pixel, previous, current);
            nearest = MathF.Min(nearest, distance);
            previous = current;
        }

        return nearest;
    }

    private Transform DragMove(Ray ray)
    {
        Vector3 direction = GetDirection(Hovered);
        if (!ray.ClosestOnLine(_start.Position, direction, out float along))
        {
            return _start;
        }

        Transform moved = _start;
        moved.Position = _start.Position + (direction * (along - _startAlong));
        return moved;
    }

    private Transform DragRotate(Ray ray)
    {
        Vector3 axis = GetDirection(Hovered);
        Vector3 centre = _start.Position;
        if (!ray.IntersectPlane(centre, axis, out Vector3 hit))
        {
            return _start;
        }

        Vector3 offset = hit - centre;
        Vector3 turn = Vector3.Cross(_startOffset, offset);
        float sine = Vector3.Dot(turn, axis);
        float cosine = Vector3.Dot(_startOffset, offset);
        float angle = MathF.Atan2(sine, cosine);
        Quaternion delta = Quaternion.CreateFromAxisAngle(axis, angle);
        Transform rotated = _start;
        rotated.Rotation = Quaternion.Normalize(Quaternion.Concatenate(_start.Rotation, delta));
        return rotated;
    }

    private Transform DragScale(Ray ray, Vector2 pixel)
    {
        Transform scaled = _start;
        if (Hovered == GizmoAxis.All)
        {
            Vector2 moved = pixel - _startPixel;
            float factor = 1 + ((moved.X - moved.Y) / PixelsPerUniformScale);
            Vector3 uniform = _start.Scale * factor;
            scaled.Scale = Vector3.Max(uniform, new Vector3(MinScale));
            return scaled;
        }

        Vector3 direction = GetDirection(Hovered);
        if (!ray.ClosestOnLine(_start.Position, direction, out float along))
        {
            return _start;
        }

        float axisFactor = 1 + ((along - _startAlong) / _handleLength);
        Vector3 factors = Vector3.One + (direction * (axisFactor - 1));
        Vector3 result = _start.Scale * factors;
        scaled.Scale = Vector3.Max(result, new Vector3(MinScale));
        return scaled;
    }
}
