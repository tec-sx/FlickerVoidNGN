using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// How the camera looks at the canvas, which lies on a sphere around it like a sky: the camera's right, up and forward
/// directions, how far an orthographic camera has panned along its right and up in pixels (zero for a perspective one, whose
/// pan the sphere ignores, like a sky's), and the half height of the lens the sphere is seen through, the tangent of half its
/// field of view: narrower for a less curved canvas.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CanvasView(Vector3 Right, float PanRight, Vector3 Up, float PanUp, Vector3 Forward, float LensHalfHeight)
{
    private const float FlattestLens = 5 * MathF.PI / 180;
    private const float WidestLens = 90 * MathF.PI / 180;

    public static CanvasView From(CameraView view, Camera camera, int height, float curvature)
    {
        Matrix4x4.Invert(view.View, out Matrix4x4 world);
        Vector3 right = new(world.M11, world.M12, world.M13);
        Vector3 up = new(world.M21, world.M22, world.M23);
        Vector3 forward = new(-world.M31, -world.M32, -world.M33);
        Vector3 position = world.Translation;
        float panRight = 0;
        float panUp = 0;
        if (camera.Projection == CameraProjection.Orthographic && camera.OrthographicHeight > 0)
        {
            float pixelsPerUnit = height / camera.OrthographicHeight;
            panRight = Vector3.Dot(position, right) * pixelsPerUnit;
            panUp = Vector3.Dot(position, up) * pixelsPerUnit;
        }

        float fieldOfView = float.Lerp(FlattestLens, WidestLens, Math.Clamp(curvature, 0, 1));
        float lensHalfHeight = MathF.Tan(fieldOfView / 2);
        return new CanvasView(right, panRight, up, panUp, forward, lensHalfHeight);
    }
}
