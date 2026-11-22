using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// How the camera looks at the paper, which lies on a sphere around it like a sky: the camera's right, up and forward
/// directions, and how far an orthographic camera has panned along its right and up in pixels (zero for a perspective one,
/// whose pan the sphere ignores, like a sky's).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PaperView(Vector3 Right, float PanRight, Vector3 Up, float PanUp, Vector3 Forward, float Padding)
{
    public static PaperView From(CameraView view, Camera camera, int height)
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

        return new PaperView(right, panRight, up, panUp, forward, 0);
    }
}
