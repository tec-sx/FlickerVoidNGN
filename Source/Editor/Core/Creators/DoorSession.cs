using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Creators;

/// <summary>
/// The door tool's work before Apply: a door that follows the mouse onto a wall's face and turns to its normal, then, once placed,
/// its cut box (red) with a handle on its front and back that move along the normal, the hinge arcs (green), the frame, the window
/// and the grab point (a small sphere). It holds a copy of the door's fit; nothing reaches the file until the creator applies it.
/// </summary>
public sealed class DoorSession
{
    private const float HandlePixels = 7;
    private const float PickPixels = 14;
    private const float MinDepth = 0.02f;
    private const float ShownThrough = 2;
    private const int ArcSegments = 20;

    private static readonly Vector4 Red = new(0.95f, 0.2f, 0.15f, 1);
    private static readonly Vector4 RedFill = new(0.95f, 0.2f, 0.15f, 0.22f);
    private static readonly Vector4 Green = new(0.35f, 0.85f, 0.25f, 1);
    private static readonly Vector4 Warm = new(0.95f, 0.7f, 0.3f, 1);
    private static readonly Vector4 Pale = new(0.95f, 0.92f, 0.85f, 1);
    private static readonly Vector4 Hover = new(1, 0.85f, 0.1f, 1);
    private static readonly Vector4 Blue = new(0.4f, 0.7f, 1, 1);

    private readonly EditorCamera _camera;
    private Matrix4x4 _toWorld = Matrix4x4.Identity;
    private DoorHandle _hovered;
    private DoorHandle _dragged;
    private float _dragStartParam;
    private float _dragStartValue;
    private Vector3 _dragOrigin;

    public DoorSession(EditorCamera camera, DoorFit fit, string? doorId, Matrix4x4 wallToWorld)
    {
        _camera = camera;
        Fit = fit;
        DoorId = doorId;
        _toWorld = wallToWorld;
        IsPlaced = doorId is not null;
        HasTarget = doorId is not null;
    }

    /// <summary>The door's numbers as they now stand; the options panel edits them.</summary>
    public DoorFit Fit { get; }

    /// <summary>The id of the placed door being changed, or null for a new one.</summary>
    public string? DoorId { get; }

    /// <summary>Whether a click has set the door on its wall; until then it follows the mouse.</summary>
    public bool IsPlaced { get; private set; }

    /// <summary>Whether the door stands on a wall, either under the mouse or placed.</summary>
    public bool HasTarget { get; private set; }

    public bool IsDragging => _dragged != DoorHandle.None;

    public event Action? Changed;

    /// <summary>Puts the door on the face of the wall under the pixel, standing on the wall's lowest point; false when the mouse is over no upright face.</summary>
    public bool Aim(string wallId, SolidMesh solid, Matrix4x4 toWorld, Vector2 pixel, int width, int height)
    {
        MeshPicker picker = new(solid, toWorld, _camera, width, height);
        if (picker.Pick(MeshElement.Face, pixel) is not { } hit || !Matrix4x4.Invert(toWorld, out Matrix4x4 toLocal))
        {
            HasTarget = false;
            return false;
        }

        Vector3 normal = GetFaceNormal(solid, hit.Index);
        Vector3 up = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, toLocal));
        if (MathF.Abs(Vector3.Dot(normal, up)) > 0.3f)
        {
            HasTarget = false;
            return false;
        }

        Ray ray = _camera.GetRay(pixel, width, height).Transform(toLocal);
        Vector3 onFace = solid.Points[solid.Faces[hit.Index][0]];
        float along = Vector3.Dot(ray.Direction, normal);
        if (MathF.Abs(along) < 1e-6f)
        {
            HasTarget = false;
            return false;
        }

        float distance = Vector3.Dot(onFace - ray.Origin, normal) / along;
        Vector3 point = ray.Origin + (ray.Direction * distance);
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, normal));
        Vector3 faceUp = Vector3.Cross(normal, right);
        float floor = solid.Points.Min(corner => Vector3.Dot(corner, faceUp));
        float across = Vector3.Dot(point, right);
        float rise = Vector3.Dot(point, normal);
        Fit.Wall = wallId;
        Fit.Normal = normal;
        Fit.Right = right;
        Fit.Centre = (right * across) + (faceUp * floor) + (normal * rise);
        _toWorld = toWorld;
        HasTarget = true;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Tells the viewport the numbers changed.</summary>
    public void Touch() => Changed?.Invoke();

    /// <summary>Sets the door where it is, so it stays and its handles can be dragged.</summary>
    public bool Place()
    {
        if (!HasTarget)
        {
            return false;
        }

        IsPlaced = true;
        return true;
    }

    /// <summary>Starts dragging the cut's handle under the pixel; false when none is.</summary>
    public bool BeginDrag(Vector2 pixel, int width, int height)
    {
        DoorHandle handle = FindHandle(pixel, width, height);
        if (!IsPlaced || handle == DoorHandle.None)
        {
            return false;
        }

        _dragged = handle;
        _dragOrigin = Vector3.Transform(GetHandlePoint(handle), GetFrame());
        _dragStartParam = GetParam(pixel, width, height, handle);
        _dragStartValue = GetDepth(handle);
        return true;
    }

    public void Move(Vector2 pixel, int width, int height)
    {
        if (_dragged != DoorHandle.None)
        {
            float moved = GetParam(pixel, width, height, _dragged) - _dragStartParam;
            if (_dragged == DoorHandle.Front)
            {
                Fit.DepthOut = MathF.Max(MinDepth, _dragStartValue + moved);
            }
            else
            {
                Fit.DepthIn = MathF.Max(MinDepth, _dragStartValue - moved);
            }

            Changed?.Invoke();
            return;
        }

        if (IsPlaced)
        {
            _hovered = FindHandle(pixel, width, height);
        }
    }

    public void EndDrag() => _dragged = DoorHandle.None;

    /// <summary>Adds the door, its cut and its marks to the overlay.</summary>
    public void Draw(OverlayBuilder overlay)
    {
        if (!HasTarget)
        {
            return;
        }

        Matrix4x4 frame = GetFrame();
        Vector3 doorHalf = new Vector3(Fit.Width, Fit.Height, Fit.Thickness) / 2;
        Vector3 doorCentre = (Vector3.UnitY * (Fit.Sill + (Fit.Height / 2))) - (Vector3.UnitZ * (Fit.Thickness / 2));
        Matrix4x4 doorWorld = Matrix4x4.CreateTranslation(doorCentre) * frame;
        overlay.AddBox(-doorHalf, doorHalf, doorWorld, Pale, OverlayLines.Thin);
        DrawCut(overlay, frame);
        DrawHinge(overlay, frame);
        DrawWindow(overlay, frame, doorCentre);
        DrawGrab(overlay);
    }

    private void DrawCut(OverlayBuilder overlay, in Matrix4x4 frame)
    {
        float back = GetShownBack();
        float width = Fit.Width + (Fit.Margin * 2);
        float height = Fit.Height + (Fit.Margin * 2);
        float depth = back + Fit.DepthOut;
        Vector3 half = new Vector3(width, height, depth) / 2;
        Vector3 centre = new(0, Fit.Sill + (Fit.Height / 2), (Fit.DepthOut - back) / 2);
        Matrix4x4 world = Matrix4x4.CreateTranslation(centre) * frame;
        overlay.AddBox(-half, half, world, Red, OverlayLines.Thin);
        AddFilledBox(overlay, half, world);
        if (FrameWidthShown() > 0)
        {
            float outerWidth = width + (Fit.FrameWidth * 2);
            float outerHeight = height - Fit.Margin + Fit.FrameWidth;
            Vector3 outerHalf = new Vector3(outerWidth, outerHeight, Fit.Thickness + 0.04f) / 2;
            Vector3 outerCentre = new(0, Fit.Sill + (outerHeight / 2) - Fit.Margin, -(Fit.Thickness / 2));
            overlay.AddBox(-outerHalf, outerHalf, Matrix4x4.CreateTranslation(outerCentre) * frame, Warm, OverlayLines.Thin);
        }

        if (!IsPlaced)
        {
            return;
        }

        overlay.AddCube(Vector3.Transform(GetHandlePoint(DoorHandle.Front), frame), HandlePixels, GetHandleColor(DoorHandle.Front));
        if (Fit.DepthIn > 0)
        {
            overlay.AddCube(Vector3.Transform(GetHandlePoint(DoorHandle.Back), frame), HandlePixels, GetHandleColor(DoorHandle.Back));
        }
    }

    private void DrawHinge(OverlayBuilder overlay, in Matrix4x4 frame)
    {
        float side = -1;
        if (Fit.HingeLeft)
        {
            side = 1;
        }

        float turn = side;
        if (!Fit.Outward)
        {
            turn = -turn;
        }

        // The door's hinge edge is at -side across the face; the arc follows the free edge from its closed place through the open angle.
        Vector3 pivot = new(-side * Fit.Width / 2, 0, -(Fit.Thickness / 2));
        Vector3 free = new(side * Fit.Width / 2, 0, -(Fit.Thickness / 2));
        Vector3 arm = free - pivot;
        float[] heights = [Fit.Sill + 0.02f, Fit.Sill + Fit.Height - 0.02f];
        foreach (float height in heights)
        {
            Vector3 previous = Vector3.Zero;
            for (int step = 0; step <= ArcSegments; step++)
            {
                float angle = Fit.Angle * step / ArcSegments;
                Quaternion turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -turn * float.DegreesToRadians(angle));
                Vector3 point = pivot + Vector3.Transform(arm, turned) + (Vector3.UnitY * height);
                Vector3 world = Vector3.Transform(point, frame);
                if (step > 0)
                {
                    overlay.AddLine(previous, world, Green, OverlayLines.Thin);
                }

                previous = world;
            }
        }

        Vector3 hingeTop = Vector3.Transform(pivot + (Vector3.UnitY * (Fit.Sill + Fit.Height)), frame);
        Vector3 hingeBottom = Vector3.Transform(pivot + (Vector3.UnitY * Fit.Sill), frame);
        overlay.AddLine(hingeBottom, hingeTop, Green, OverlayLines.Bold);
    }

    private void DrawWindow(OverlayBuilder overlay, in Matrix4x4 frame, Vector3 doorCentre)
    {
        if (!Fit.HasWindow)
        {
            return;
        }

        Vector3 half = new Vector3(Fit.WindowWidth, Fit.WindowHeight, Fit.Thickness + 0.1f) / 2;
        Vector3 centre = new(0, Fit.Sill + Fit.WindowFrom + (Fit.WindowHeight / 2), doorCentre.Z);
        Matrix4x4 world = Matrix4x4.CreateTranslation(centre) * frame;
        overlay.AddBox(-half, half, world, Red, OverlayLines.Thin);
        AddFilledBox(overlay, half, world);
        if (Fit.Glass)
        {
            Vector3 glass = new(Fit.WindowWidth / 2, Fit.WindowHeight / 2, 0.005f);
            overlay.AddBox(-glass, glass, world, Blue, 1);
        }
    }

    private void DrawGrab(OverlayBuilder overlay)
    {
        Vector3 grab = Vector3.Transform(Fit.GetGrab(), _toWorld);
        float radius = _camera.WorldUnitsPerPixel(grab, 1000) * 7;
        overlay.AddCube(grab, 3, Hover);
        overlay.AddCircle(grab, Vector3.UnitX, radius, Hover, 1);
        overlay.AddCircle(grab, Vector3.UnitY, radius, Hover, 1);
        overlay.AddCircle(grab, Vector3.UnitZ, radius, Hover, 1);
    }

    private static void AddFilledBox(OverlayBuilder overlay, Vector3 half, in Matrix4x4 world)
    {
        Vector3[] corners = new Vector3[8];
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 local = new(
                (corner & 1) == 0 ? -half.X : half.X,
                (corner & 2) == 0 ? -half.Y : half.Y,
                (corner & 4) == 0 ? -half.Z : half.Z);
            corners[corner] = Vector3.Transform(local, world);
        }

        int[] faces = [0, 1, 3, 2, 4, 6, 7, 5, 0, 4, 5, 1, 2, 3, 7, 6, 0, 2, 6, 4, 1, 5, 7, 3];
        for (int face = 0; face < faces.Length; face += 4)
        {
            overlay.AddTriangle(corners[faces[face]], corners[faces[face + 1]], corners[faces[face + 2]], RedFill);
            overlay.AddTriangle(corners[faces[face]], corners[faces[face + 2]], corners[faces[face + 3]], RedFill);
        }
    }

    // The wall space basis of the door: right, up and normal as columns, with the door's foot as the origin; carries into the world.
    private Matrix4x4 GetFrame()
    {
        Vector3 up = Fit.Up;
        Matrix4x4 basis = new(
            Fit.Right.X, Fit.Right.Y, Fit.Right.Z, 0,
            up.X, up.Y, up.Z, 0,
            Fit.Normal.X, Fit.Normal.Y, Fit.Normal.Z, 0,
            Fit.Centre.X, Fit.Centre.Y, Fit.Centre.Z, 1);
        return basis * _toWorld;
    }

    private float GetShownBack()
    {
        if (Fit.DepthIn > 0)
        {
            return Fit.DepthIn;
        }

        return ShownThrough;
    }

    private float FrameWidthShown() => Fit.FrameWidth;

    private Vector3 GetHandlePoint(DoorHandle handle)
    {
        float z = Fit.DepthOut;
        if (handle == DoorHandle.Back)
        {
            z = -GetShownBack();
        }

        return new Vector3(0, Fit.Sill + (Fit.Height / 2), z);
    }

    private float GetDepth(DoorHandle handle)
    {
        if (handle == DoorHandle.Front)
        {
            return Fit.DepthOut;
        }

        return Fit.DepthIn;
    }

    private Vector4 GetHandleColor(DoorHandle handle)
    {
        if (_dragged == handle || (_dragged == DoorHandle.None && _hovered == handle))
        {
            return Hover;
        }

        return Red;
    }

    private DoorHandle FindHandle(Vector2 pixel, int width, int height)
    {
        Matrix4x4 frame = GetFrame();
        Vector2 front = _camera.Project(Vector3.Transform(GetHandlePoint(DoorHandle.Front), frame), width, height);
        Vector2 back = _camera.Project(Vector3.Transform(GetHandlePoint(DoorHandle.Back), frame), width, height);
        float frontDistance = Vector2.Distance(front, pixel);
        float backDistance = float.MaxValue;
        if (Fit.DepthIn > 0)
        {
            backDistance = Vector2.Distance(back, pixel);
        }

        if (frontDistance <= PickPixels && frontDistance <= backDistance)
        {
            return DoorHandle.Front;
        }

        if (backDistance <= PickPixels)
        {
            return DoorHandle.Back;
        }

        return DoorHandle.None;
    }

    // Where along the normal, in the wall's units, the mouse ray comes closest to the handle's line.
    private float GetParam(Vector2 pixel, int width, int height, DoorHandle handle)
    {
        Matrix4x4 frame = GetFrame();
        Vector3 origin = _dragOrigin;
        Vector3 axis = Vector3.TransformNormal(Vector3.UnitZ, frame);
        float scale = axis.Length();
        axis = Vector3.Normalize(axis);
        Ray ray = _camera.GetRay(pixel, width, height);
        Vector3 between = origin - ray.Origin;
        float dd = Vector3.Dot(ray.Direction, axis);
        float denominator = 1 - (dd * dd);
        if (denominator < 1e-6f)
        {
            return 0;
        }

        float rayAlong = Vector3.Dot(between, ray.Direction);
        float axisAlong = Vector3.Dot(between, axis);
        float onAxis = ((dd * rayAlong) - axisAlong) / denominator;
        return onAxis / scale;
    }

    private static Vector3 GetFaceNormal(SolidMesh solid, int face)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 sum = Vector3.Zero;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            Vector3 current = solid.Points[corners[corner]];
            Vector3 next = solid.Points[corners[(corner + 1) % corners.Count]];
            sum += Vector3.Cross(current, next);
        }

        return Vector3.Normalize(sum);
    }

    private enum DoorHandle
    {
        None,
        Front,
        Back,
    }
}
