using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// Where the flat canvas lies on the screen: how many pixels high its image is, and how far the camera has panned along its
/// right and up in pixels. An orthographic camera moves everything by the same pixels, so the canvas slides and scales with
/// the world and stays under the paint. The pan is the camera's position along its own right and up, so a camera turning
/// where it stands leaves the canvas still; a perspective camera always does.
/// </summary>
internal readonly record struct CanvasView(float ImageHeight, float PanRight, float PanUp)
{
    /// <summary>The orthographic height at which the image repeats its tiling's times up the screen.</summary>
    public const float ReferenceHeight = 10;

    public static CanvasView From(CameraView view, Camera camera, int height, float tiling)
    {
        float imageHeight = height / Math.Max(tiling, 0.01f);
        if (camera.Projection != CameraProjection.Orthographic || camera.OrthographicHeight <= 0)
        {
            return new CanvasView(imageHeight, 0, 0);
        }

        Matrix4x4.Invert(view.View, out Matrix4x4 world);
        Vector3 right = new(world.M11, world.M12, world.M13);
        Vector3 up = new(world.M21, world.M22, world.M23);
        Vector3 position = world.Translation;
        float pixelsPerUnit = height / camera.OrthographicHeight;
        float panRight = Vector3.Dot(position, right) * pixelsPerUnit;
        float panUp = Vector3.Dot(position, up) * pixelsPerUnit;
        float zoomedHeight = imageHeight * ReferenceHeight / camera.OrthographicHeight;
        return new CanvasView(zoomedHeight, panRight, panUp);
    }
}
