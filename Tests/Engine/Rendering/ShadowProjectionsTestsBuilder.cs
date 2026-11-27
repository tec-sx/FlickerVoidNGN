using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Shadows;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds sun shadow projections for a camera at z = 10 looking down -Z, 10 units high or with a 60 degree view.</summary>
internal sealed class ShadowProjectionsTestsBuilder
{
    public const float ShadowDistance = 20;
    public const float CasterReach = 30;

    private Camera _camera = new() { OrthographicHeight = 10, FieldOfView = 60, NearPlane = 0.1f, FarPlane = 100 };
    private Vector3 _position = new(0, 0, 10);
    private Bounds _world = new(new Vector3(-1000), new Vector3(1000));

    public ShadowProjectionsTestsBuilder WithProjection(CameraProjection projection)
    {
        _camera = _camera with { Projection = projection };
        return this;
    }

    public ShadowProjectionsTestsBuilder WithCameraOffset(Vector3 offset)
    {
        _position += offset;
        return this;
    }

    /// <summary>Without a call, the world reaches far beyond the view.</summary>
    public ShadowProjectionsTestsBuilder WithWorld(Bounds world)
    {
        _world = world;
        return this;
    }

    public Matrix4x4 FitSun(Vector3 sunDirection)
    {
        Matrix4x4? shadow = TryFitSun(sunDirection);
        return shadow ?? throw new InvalidOperationException("The view sees nothing of the world.");
    }

    public Matrix4x4? TryFitSun(Vector3 sunDirection)
    {
        CameraView view = BuildView();
        return ShadowProjections.FitSun(_camera, view, sunDirection, ShadowDistance, CasterReach, _world);
    }

    /// <summary>How many shadow map texels a world unit across the light covers.</summary>
    public static float Detail(Matrix4x4 shadow)
    {
        Vector3 xAxis = new(shadow.M11, shadow.M21, shadow.M31);
        return xAxis.Length() * ShadowAtlas.SunTileSize / 2;
    }

    /// <summary>The corners of the camera's view, cut at the shadow distance.</summary>
    public IEnumerable<Vector3> ViewCorners()
    {
        CameraView view = BuildView();
        Matrix4x4.Invert(view.ViewProjection, out Matrix4x4 toWorld);
        float farDepth = ProjectDepth(view, ShadowDistance);
        foreach (float z in new[] { 0, farDepth })
        {
            foreach (float y in new[] { -1f, 1f })
            {
                foreach (float x in new[] { -1f, 1f })
                {
                    yield return Project(toWorld, new Vector3(x, y, z));
                }
            }
        }
    }

    /// <summary>The point after the matrix and the perspective divide.</summary>
    public static Vector3 Project(Matrix4x4 matrix, Vector3 point)
    {
        Vector4 clip = Vector4.Transform(point, matrix);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    private static float ProjectDepth(CameraView view, float depth)
    {
        Vector4 clip = Vector4.Transform(new Vector4(0, 0, -depth, 1), view.Projection);
        return clip.Z / clip.W;
    }

    private CameraView BuildView()
    {
        Transform transform = new() { Position = _position };
        return CameraView.Create(_camera, transform, 1600, 900);
    }
}
