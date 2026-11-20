namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>A view into the world from its entity's transform, looking along the entity's -Z with +Y up.</summary>
public struct Camera
{
    public Camera()
    {
    }

    public CameraProjection Projection { get; set; }

    /// <summary>How many world units the view shows from bottom to top, for an orthographic camera.</summary>
    public float OrthographicHeight { get; set; } = 10;

    /// <summary>The vertical field of view in degrees, for a perspective camera.</summary>
    public float FieldOfView { get; set; } = 60;

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 100;
}

