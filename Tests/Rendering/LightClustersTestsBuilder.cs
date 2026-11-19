using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds light clusters for a camera at z = 5 looking down -Z on a 16:9 viewport, so each tile is square. By default the
/// camera is orthographic and 9 units high, which makes every tile one unit wide.
/// </summary>
internal sealed class LightClustersTestsBuilder
{
    public const float Near = 0.1f;
    public const float Far = 100;
    public const float CameraZ = 5;

    private readonly List<LightBounds> _lights = [];
    private Camera _camera = new() { OrthographicHeight = 9, NearPlane = Near, FarPlane = Far };

    public LightClustersTestsBuilder WithPerspective(float fieldOfViewDegrees)
    {
        _camera = _camera with { Projection = CameraProjection.Perspective, FieldOfView = fieldOfViewDegrees };
        return this;
    }

    public LightClustersTestsBuilder WithLight(Vector3 position, float range)
    {
        _lights.Add(new LightBounds(position, range));
        return this;
    }

    public LightClusters Build()
    {
        Transform transform = new() { Position = new Vector3(0, 0, CameraZ) };
        CameraView view = CameraView.Create(_camera, transform, 1600, 900);
        LightClusters clusters = new();
        clusters.Build(_camera, view, _lights.ToArray());
        return clusters;
    }
}
