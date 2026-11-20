using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;

namespace FlickerVoid.Editor.Core.Tests.Cameras;

/// <summary>Builds an editor camera looking at a pivot from a yaw, pitch and distance; by default it looks at the origin from 10 units.</summary>
internal sealed class EditorCameraTestsBuilder
{
    public const float Precision = 1e-4f;

    private Vector3 _pivot;
    private float _yaw;
    private float _pitch;
    private float _distance = 10;

    public EditorCameraTestsBuilder LookingAt(Vector3 pivot, float yaw, float pitch, float distance)
    {
        _pivot = pivot;
        _yaw = yaw;
        _pitch = pitch;
        _distance = distance;
        return this;
    }

    /// <summary>Looks straight down from just short of the limit, so +X is right on screen and -Z is up.</summary>
    public EditorCameraTestsBuilder LookingDown(float distance)
    {
        return LookingAt(Vector3.Zero, 0, -EditorCamera.PitchLimit, distance);
    }

    public EditorCamera Build()
    {
        EditorCamera camera = new();
        camera.LookAt(_pivot, _yaw, _pitch, _distance);
        return camera;
    }
}
