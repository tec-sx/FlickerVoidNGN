using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering;

/// <summary>A camera's view and projection matrices for one viewport, in System.Numerics' right-handed convention with depth from 0 to 1.</summary>
public readonly record struct CameraView(Matrix4x4 View, Matrix4x4 Projection)
{
    public Matrix4x4 ViewProjection => View * Projection;

    /// <summary>
    /// Creates the view from the camera's transform, ignoring its scale. An orthographic view moves in whole pixels, so the
    /// scene doesn't shimmer as the camera glides.
    /// </summary>
    public static CameraView Create(in Camera camera, in Transform transform, int width, int height)
    {
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(transform.Rotation);
        Matrix4x4 translation = Matrix4x4.CreateTranslation(transform.Position);
        Matrix4x4.Invert(rotation * translation, out Matrix4x4 view);
        float aspect = (float)width / height;

        Matrix4x4 projection;
        switch (camera.Projection)
        {
            case CameraProjection.Perspective:
                float fieldOfView = float.DegreesToRadians(camera.FieldOfView);
                projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspect, camera.NearPlane, camera.FarPlane);
                break;
            default:
                float unitsPerPixel = camera.OrthographicHeight / height;
                view.M41 = MathF.Round(view.M41 / unitsPerPixel) * unitsPerPixel;
                view.M42 = MathF.Round(view.M42 / unitsPerPixel) * unitsPerPixel;
                projection = Matrix4x4.CreateOrthographic(camera.OrthographicHeight * aspect, camera.OrthographicHeight, camera.NearPlane, camera.FarPlane);
                break;
        }

        return new CameraView(view, projection);
    }
}
