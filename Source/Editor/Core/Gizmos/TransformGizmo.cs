using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Gizmos;

/// <summary>
/// The handles that move, rotate and scale a transform along the world axes or the transform's own (<see cref="Space"/>; scale
/// handles always follow the transform's own, as its scale is along them): arrows, rings or cube-tipped lines that keep their
/// size on screen, and for moving and scaling a small square in the corner between each two handles that moves or scales along
/// both, coloured as the axis it stands across, as in Unity and Godot. Finds the handle under the mouse, works out the transform as a handle is dragged, and draws itself. While
/// snapping is on, a move puts the dragged world axis on the grid (or moves in grid steps along a local one), a turn goes in
/// steps of degrees, and a scale in steps of the scale the drag started from.
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
    private const float LineWidth = OverlayLines.Handle;
    private const float ArrowShaft = 0.8f;
    private const float ConeRadius = 6;
    private const float ScaleShaft = 0.9f;
    private const float TipCube = 5;
    private const float CentreCube = 7;
    private const float MinScale = 0.001f;

    /// <summary>Where a plane handle's square starts and ends along both its axes, in handle lengths.</summary>
    private const float PlaneNear = 0.15f;
    private const float PlaneFar = 0.3f;
    private const float PlaneFill = 0.35f;

    private static readonly GizmoAxis[] Planes = [GizmoAxis.XY, GizmoAxis.YZ, GizmoAxis.XZ];

    private static readonly Vector4 XColor = new(0.9f, 0.15f, 0.15f, 1);
    private static readonly Vector4 YColor = new(0.3f, 0.85f, 0.2f, 1);
    private static readonly Vector4 ZColor = new(0.15f, 0.35f, 1, 1);
    private static readonly Vector4 CentreColor = new(0.9f, 0.9f, 0.9f, 1);
    private static readonly Vector4 HoverColor = new(1, 0.85f, 0.1f, 1);

    private Transform _start;
    private Vector2 _startPixel;
    private float _startAlong;
    private Vector3 _startOffset;
    private Vector3 _startHit;
    private float _handleLength;
    private Quaternion _orientation = Quaternion.Identity;

    public GizmoMode Mode { get; set; } = GizmoMode.Move;

    public GizmoSpace Space { get; set; } = GizmoSpace.World;

    /// <summary>The grid moves snap to, in world units.</summary>
    public GizmoSnap MoveSnap { get; } = new([0.1f, 0.25f, 0.5f, 1, 2, 5], 0.5f);

    /// <summary>The steps turns snap to, in degrees.</summary>
    public GizmoSnap RotateSnap { get; } = new([5, 10, 15, 30, 45, 90], 15);

    /// <summary>The steps scaling snaps to, as shares of the starting scale.</summary>
    public GizmoSnap ScaleSnap { get; } = new([0.1f, 0.25f, 0.5f, 1], 0.25f);

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
            Quaternion orientation = GetOrientation(target);
            Hovered = FindAxis(camera, target.Position, orientation, pixel, width, height);
        }

        return Hovered;
    }

    /// <summary>Starts dragging the handle under the mouse; false when there is none.</summary>
    public bool BeginDrag(EditorCamera camera, in Transform target, Vector2 pixel, int width, int height)
    {
        Quaternion orientation = GetOrientation(target);
        GizmoAxis axis = FindAxis(camera, target.Position, orientation, pixel, width, height);
        if (axis == GizmoAxis.None)
        {
            return false;
        }

        Vector3 centre = target.Position;
        Ray ray = camera.GetRay(pixel, width, height);
        Vector3 direction = GetDirection(axis, orientation);
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
                if (IsPlane(axis))
                {
                    Vector3 normal = GetPlaneNormal(axis, orientation);
                    if (!ray.IntersectPlane(centre, normal, out _startHit))
                    {
                        return false;
                    }

                    break;
                }

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
        _orientation = orientation;
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
            case GizmoMode.Move when IsPlane(Hovered):
                return DragOnPlane(ray);
            case GizmoMode.Move:
                return DragMove(ray);
            case GizmoMode.Rotate:
                return DragRotate(ray);
            case GizmoMode.Scale when IsPlane(Hovered):
                return DragScaleOnPlane(ray);
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
        Quaternion orientation = GetOrientation(target);
        float length = GetHandleLength(camera, centre, viewportHeight);
        GizmoAxis[] axes = [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];
        foreach (GizmoAxis axis in axes)
        {
            Vector3 direction = GetDirection(axis, orientation);
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

        if (Mode is GizmoMode.Move or GizmoMode.Scale)
        {
            DrawPlanes(overlay, centre, orientation, length);
        }
    }

    /// <summary>Each plane handle as a see-through square with a solid edge.</summary>
    private void DrawPlanes(OverlayBuilder overlay, Vector3 centre, Quaternion orientation, float length)
    {
        foreach (GizmoAxis plane in Planes)
        {
            Vector3[] corners = GetPlaneCorners(plane, centre, orientation, length);
            Vector4 color = GetColor(plane);
            Vector4 fill = color with { W = PlaneFill };
            overlay.AddTriangle(corners[0], corners[1], corners[2], fill);
            overlay.AddTriangle(corners[0], corners[2], corners[3], fill);
            for (int index = 0; index < corners.Length; index++)
            {
                Vector3 next = corners[(index + 1) % corners.Length];
                overlay.AddLine(corners[index], next, color, 1);
            }
        }
    }

    /// <summary>The square of a plane handle, going round it from the corner nearest the centre.</summary>
    private static Vector3[] GetPlaneCorners(GizmoAxis plane, Vector3 centre, Quaternion orientation, float length)
    {
        (GizmoAxis first, GizmoAxis second) = GetPlaneAxes(plane);
        Vector3 along = GetDirection(first, orientation) * length;
        Vector3 across = GetDirection(second, orientation) * length;
        return
        [
            centre + (along * PlaneNear) + (across * PlaneNear),
            centre + (along * PlaneFar) + (across * PlaneNear),
            centre + (along * PlaneFar) + (across * PlaneFar),
            centre + (along * PlaneNear) + (across * PlaneFar),
        ];
    }

    private static bool IsPlane(GizmoAxis axis) => axis is GizmoAxis.XY or GizmoAxis.YZ or GizmoAxis.XZ;

    /// <summary>The two axes a plane handle moves along.</summary>
    private static (GizmoAxis First, GizmoAxis Second) GetPlaneAxes(GizmoAxis plane)
    {
        switch (plane)
        {
            case GizmoAxis.XY:
                return (GizmoAxis.X, GizmoAxis.Y);
            case GizmoAxis.YZ:
                return (GizmoAxis.Y, GizmoAxis.Z);
            default:
                return (GizmoAxis.X, GizmoAxis.Z);
        }
    }

    /// <summary>The axis a plane handle stands across, which names its colour.</summary>
    private static GizmoAxis GetPlaneNormalAxis(GizmoAxis plane)
    {
        switch (plane)
        {
            case GizmoAxis.XY:
                return GizmoAxis.Z;
            case GizmoAxis.YZ:
                return GizmoAxis.X;
            default:
                return GizmoAxis.Y;
        }
    }

    private static Vector3 GetPlaneNormal(GizmoAxis plane, Quaternion orientation)
    {
        GizmoAxis normal = GetPlaneNormalAxis(plane);
        return GetDirection(normal, orientation);
    }

    private static bool IsInside(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int index = 0, previous = polygon.Length - 1; index < polygon.Length; previous = index++)
        {
            Vector2 a = polygon[index];
            Vector2 b = polygon[previous];
            bool crosses = (a.Y > point.Y) != (b.Y > point.Y);
            if (crosses && point.X < ((b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>The world-space direction of an axis's handle on a gizmo turned by <paramref name="orientation"/>.</summary>
    private static Vector3 GetDirection(GizmoAxis axis, Quaternion orientation)
    {
        Vector3 unit = GetUnit(axis);
        return Vector3.Transform(unit, orientation);
    }

    private static Vector3 GetUnit(GizmoAxis axis)
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

        if (IsPlane(axis))
        {
            axis = GetPlaneNormalAxis(axis);
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

    /// <summary>How the handles are turned over a target: not at all in world space, by its rotation in local space or for scale.</summary>
    private Quaternion GetOrientation(in Transform target)
    {
        if (Space == GizmoSpace.Local || Mode == GizmoMode.Scale)
        {
            return target.Rotation;
        }

        return Quaternion.Identity;
    }

    private GizmoAxis FindAxis(EditorCamera camera, Vector3 centre, Quaternion orientation, Vector2 pixel, int width, int height)
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
            Vector3 direction = GetDirection(axis, orientation);
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

        // The arrows win where a square shows behind or over them, so a grab on an arrow always moves along it.
        if (nearest == GizmoAxis.None && (Mode is GizmoMode.Move or GizmoMode.Scale) && FindPlane(camera, centre, orientation, length, pixel, width, height) is { } plane)
        {
            return plane;
        }

        return nearest;
    }

    /// <summary>The plane handle whose square the mouse is over, or null.</summary>
    private static GizmoAxis? FindPlane(EditorCamera camera, Vector3 centre, Quaternion orientation, float length, Vector2 pixel, int width, int height)
    {
        foreach (GizmoAxis plane in Planes)
        {
            Vector3[] corners = GetPlaneCorners(plane, centre, orientation, length);
            Vector2[] shown = new Vector2[corners.Length];
            for (int index = 0; index < corners.Length; index++)
            {
                shown[index] = camera.Project(corners[index], width, height);
            }

            if (IsInside(pixel, shown))
            {
                return plane;
            }
        }

        return null;
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
        Vector3 direction = GetDirection(Hovered, _orientation);
        if (!ray.ClosestOnLine(_start.Position, direction, out float along))
        {
            return _start;
        }

        Transform moved = _start;
        float distance = along - _startAlong;
        if (Space == GizmoSpace.Local)
        {
            float steps = MoveSnap.Apply(distance);
            moved.Position = _start.Position + (direction * steps);
            return moved;
        }

        Vector3 position = _start.Position + (direction * distance);
        float onAxis = Vector3.Dot(position, direction);
        float snapped = MoveSnap.Apply(onAxis);
        moved.Position = position + (direction * (snapped - onAxis));
        return moved;
    }

    /// <summary>Moves on the dragged plane handle's plane, snapping along each of its axes as a drag along that axis does.</summary>
    private Transform DragOnPlane(Ray ray)
    {
        Vector3 normal = GetPlaneNormal(Hovered, _orientation);
        if (!ray.IntersectPlane(_start.Position, normal, out Vector3 hit))
        {
            return _start;
        }

        Vector3 offset = hit - _startHit;
        (GizmoAxis first, GizmoAxis second) = GetPlaneAxes(Hovered);
        Transform moved = _start;
        moved.Position = SnapAlong(_start.Position, offset, first);
        Vector3 rest = moved.Position - _start.Position;
        moved.Position = SnapAlong(moved.Position, offset - rest, second);
        return moved;
    }

    /// <summary>Moves <paramref name="from"/> by the share of <paramref name="offset"/> along the axis, snapped.</summary>
    private Vector3 SnapAlong(Vector3 from, Vector3 offset, GizmoAxis axis)
    {
        Vector3 direction = GetDirection(axis, _orientation);
        float distance = Vector3.Dot(offset, direction);
        if (Space == GizmoSpace.Local)
        {
            float steps = MoveSnap.Apply(distance);
            return from + (direction * steps);
        }

        Vector3 position = from + (direction * distance);
        float onAxis = Vector3.Dot(position, direction);
        float snapped = MoveSnap.Apply(onAxis);
        return position + (direction * (snapped - onAxis));
    }

    /// <summary>Scales both axes of the dragged plane handle alike, by how far the drag went along their diagonal.</summary>
    private Transform DragScaleOnPlane(Ray ray)
    {
        Vector3 normal = GetPlaneNormal(Hovered, _orientation);
        if (!ray.IntersectPlane(_start.Position, normal, out Vector3 hit))
        {
            return _start;
        }

        (GizmoAxis first, GizmoAxis second) = GetPlaneAxes(Hovered);
        Vector3 firstDirection = GetDirection(first, _orientation);
        Vector3 secondDirection = GetDirection(second, _orientation);
        Vector3 diagonal = Vector3.Normalize(firstDirection + secondDirection);
        float along = Vector3.Dot(hit - _startHit, diagonal);
        float dragged = 1 + (along / _handleLength);
        float factor = ScaleSnap.Apply(dragged);
        Vector3 units = GetUnit(first) + GetUnit(second);
        Vector3 factors = Vector3.One + (units * (factor - 1));
        Transform scaled = _start;
        Vector3 result = _start.Scale * factors;
        scaled.Scale = Vector3.Max(result, new Vector3(MinScale));
        return scaled;
    }

    private Transform DragRotate(Ray ray)
    {
        Vector3 axis = GetDirection(Hovered, _orientation);
        Vector3 centre = _start.Position;
        if (!ray.IntersectPlane(centre, axis, out Vector3 hit))
        {
            return _start;
        }

        Vector3 offset = hit - centre;
        Vector3 turn = Vector3.Cross(_startOffset, offset);
        float sine = Vector3.Dot(turn, axis);
        float cosine = Vector3.Dot(_startOffset, offset);
        float degrees = float.RadiansToDegrees(MathF.Atan2(sine, cosine));
        float snapped = RotateSnap.Apply(degrees);
        float angle = float.DegreesToRadians(snapped);
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
            float dragged = 1 + ((moved.X - moved.Y) / PixelsPerUniformScale);
            float factor = ScaleSnap.Apply(dragged);
            Vector3 uniform = _start.Scale * factor;
            scaled.Scale = Vector3.Max(uniform, new Vector3(MinScale));
            return scaled;
        }

        Vector3 direction = GetDirection(Hovered, _orientation);
        if (!ray.ClosestOnLine(_start.Position, direction, out float along))
        {
            return _start;
        }

        float axisDragged = 1 + ((along - _startAlong) / _handleLength);
        float axisFactor = ScaleSnap.Apply(axisDragged);
        Vector3 unit = GetUnit(Hovered);
        Vector3 factors = Vector3.One + (unit * (axisFactor - 1));
        Vector3 result = _start.Scale * factors;
        scaled.Scale = Vector3.Max(result, new Vector3(MinScale));
        return scaled;
    }
}
