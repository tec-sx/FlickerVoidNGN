using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds the view of an orthographic camera 10 units high, looking down -Z from the origin by default, on a square viewport.</summary>
internal sealed class CameraViewTestsBuilder
{
    public const float Near = 0.5f;
    public const float Far = 50;

    private Transform _transform = new();
    private int _width = 100;
    private int _height = 100;
    private Camera _camera = new() { OrthographicHeight = 10, NearPlane = Near, FarPlane = Far };

    public CameraViewTestsBuilder AtDistance(float distance)
    {
        Vector3 position = new(0, 0, distance);
        return At(position, Quaternion.Identity);
    }

    public CameraViewTestsBuilder At(Vector3 position, Quaternion rotation)
    {
        _transform = new Transform { Position = position, Rotation = rotation };
        return this;
    }

    public CameraViewTestsBuilder WithViewport(int width, int height)
    {
        _width = width;
        _height = height;
        return this;
    }

    public CameraViewTestsBuilder WithPerspective(float fieldOfViewDegrees)
    {
        _camera = _camera with { Projection = CameraProjection.Perspective, FieldOfView = fieldOfViewDegrees };
        return this;
    }

    public CameraView Build()
    {
        return CameraView.Create(_camera, _transform, _width, _height);
    }

    /// <summary>The point in clip space, which an orthographic projection leaves undivided.</summary>
    public static Vector3 Project(CameraView view, Vector3 point)
    {
        Vector4 clip = ProjectHomogeneous(view, point);
        return new Vector3(clip.X, clip.Y, clip.Z);
    }

    public static Vector4 ProjectHomogeneous(CameraView view, Vector3 point) => Vector4.Transform(point, view.ViewProjection);
}
