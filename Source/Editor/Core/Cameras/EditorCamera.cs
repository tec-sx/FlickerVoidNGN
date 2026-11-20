using System.Numerics;

using FlickerVoid.Editor.Core.Geometry;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Cameras;

/// <summary>
/// The viewport's camera, outside the edited world: it stands at a position looking at a pivot a distance ahead, turns in place,
/// orbits and dollies round the pivot, pans across the screen and flies. Both projections frame the same height at the pivot.
/// </summary>
public sealed class EditorCamera
{
    /// <summary>Just short of straight up or down, where yaw would stop meaning anything.</summary>
    public const float PitchLimit = 1.55f;

    public const float MinPivotDistance = 0.1f;

    /// <summary>How much of the distance to the pivot one step of dollying in keeps.</summary>
    private const float DollyPerStep = 0.85f;

    private const float NearPlane = 0.05f;
    private const float FarPlane = 500;

    public Vector3 Position { get; private set; }

    /// <summary>Turn round the world's up axis in radians; zero looks along -Z, and turning left is positive.</summary>
    public float Yaw { get; private set; }

    /// <summary>Tilt in radians; negative looks down.</summary>
    public float Pitch { get; private set; }

    public float PivotDistance { get; private set; } = 10;

    public CameraProjection Projection { get; set; } = CameraProjection.Perspective;

    /// <summary>The vertical field of view in degrees, which also sets how much an orthographic view shows at the pivot.</summary>
    public float FieldOfView { get; set; } = 60;

    /// <summary>World units a second.</summary>
    public float FlySpeed { get; set; } = 5;

    public Quaternion Rotation => Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0);

    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);

    public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);

    public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);

    public Vector3 Pivot => Position + (Forward * PivotDistance);

    /// <summary>How many world units the view shows from bottom to top at the pivot.</summary>
    public float ViewHeight => 2 * PivotDistance * MathF.Tan(float.DegreesToRadians(FieldOfView) / 2);

    public void LookAt(Vector3 pivot, float yaw, float pitch, float distance)
    {
        Yaw = yaw;
        Pitch = Math.Clamp(pitch, -PitchLimit, PitchLimit);
        PivotDistance = Math.Max(distance, MinPivotDistance);
        Position = pivot - (Forward * PivotDistance);
    }

    public void Turn(float yaw, float pitch)
    {
        Yaw += yaw;
        Pitch = Math.Clamp(Pitch + pitch, -PitchLimit, PitchLimit);
    }

    public void Orbit(float yaw, float pitch)
    {
        Vector3 pivot = Pivot;
        Turn(yaw, pitch);
        Position = pivot - (Forward * PivotDistance);
    }

    /// <summary>Moves toward the pivot by whole or partial steps, or away from it with negative steps.</summary>
    public void Dolly(float steps)
    {
        Vector3 pivot = Pivot;
        float distance = PivotDistance * MathF.Pow(DollyPerStep, steps);
        PivotDistance = Math.Max(distance, MinPivotDistance);
        Position = pivot - (Forward * PivotDistance);
    }

    /// <summary>Moves across the screen so the ground at the pivot follows a drag of the mouse, given in pixels with y down.</summary>
    public void Pan(Vector2 pixels, int viewportHeight)
    {
        float unitsPerPixel = ViewHeight / viewportHeight;
        Vector3 across = Right * (-pixels.X * unitsPerPixel);
        Vector3 along = Up * (pixels.Y * unitsPerPixel);
        Position += across + along;
    }

    /// <summary>Flies along a direction with x to the right and z forward on the view, and y up in the world.</summary>
    public void Fly(Vector3 direction, float seconds)
    {
        if (direction == Vector3.Zero)
        {
            return;
        }

        Vector3 unit = Vector3.Normalize(direction);
        Vector3 move = (Right * unit.X) + (Vector3.UnitY * unit.Y) + (Forward * unit.Z);
        Position += move * (FlySpeed * seconds);
    }

    public Camera ToCamera()
    {
        return new Camera
        {
            Projection = Projection,
            FieldOfView = FieldOfView,
            OrthographicHeight = ViewHeight,
            NearPlane = NearPlane,
            FarPlane = FarPlane,
        };
    }

    /// <summary>Where the renderer's camera stands: here, or for an orthographic view half the far plane back from the pivot, so nothing near the pivot is clipped.</summary>
    public Transform ToTransform()
    {
        Vector3 eye = Position;
        if (Projection == CameraProjection.Orthographic)
        {
            eye = Pivot - (Forward * (FarPlane / 2));
        }

        return new Transform { Position = eye, Rotation = Rotation };
    }

    /// <summary>The ray from the camera through a point of the viewport given in pixels from its top left.</summary>
    public Ray GetRay(Vector2 pixel, int width, int height)
    {
        Matrix4x4 viewProjection = GetViewProjection(width, height);
        Matrix4x4.Invert(viewProjection, out Matrix4x4 inverse);
        float x = (2 * pixel.X / width) - 1;
        float y = 1 - (2 * pixel.Y / height);
        Vector4 nearClip = new(x, y, 0, 1);
        Vector4 farClip = new(x, y, 1, 1);
        Vector4 near = Vector4.Transform(nearClip, inverse);
        Vector4 far = Vector4.Transform(farClip, inverse);
        Vector3 nearPoint = new Vector3(near.X, near.Y, near.Z) / near.W;
        Vector3 farPoint = new Vector3(far.X, far.Y, far.Z) / far.W;
        Vector3 direction = Vector3.Normalize(farPoint - nearPoint);
        return new Ray(nearPoint, direction);
    }

    /// <summary>The viewport pixel, from its top left, a world point shows at.</summary>
    public Vector2 Project(Vector3 point, int width, int height)
    {
        Matrix4x4 viewProjection = GetViewProjection(width, height);
        Vector4 position = new(point, 1);
        Vector4 clip = Vector4.Transform(position, viewProjection);
        float x = clip.X / clip.W;
        float y = clip.Y / clip.W;
        return new Vector2((x + 1) * width / 2, (1 - y) * height / 2);
    }

    /// <summary>How many world units one pixel of a viewport of the given height covers at a point, so editor shapes keep their size on screen.</summary>
    public float WorldUnitsPerPixel(Vector3 point, int viewportHeight)
    {
        if (Projection == CameraProjection.Orthographic)
        {
            return ViewHeight / viewportHeight;
        }

        Vector3 toPoint = point - Position;
        float depth = MathF.Max(Vector3.Dot(toPoint, Forward), NearPlane);
        float halfAngle = float.DegreesToRadians(FieldOfView) / 2;
        return 2 * depth * MathF.Tan(halfAngle) / viewportHeight;
    }

    /// <summary>The direction from a point back toward the camera, which editor shapes face.</summary>
    public Vector3 GetDirectionToEye(Vector3 point)
    {
        if (Projection == CameraProjection.Orthographic)
        {
            return -Forward;
        }

        Vector3 toEye = Position - point;
        return Vector3.Normalize(toEye);
    }

    private Matrix4x4 GetViewProjection(int width, int height)
    {
        Camera camera = ToCamera();
        Transform transform = ToTransform();
        CameraView view = CameraView.Create(camera, transform, width, height);
        return view.ViewProjection;
    }
}
