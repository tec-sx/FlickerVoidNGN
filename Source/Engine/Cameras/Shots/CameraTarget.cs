namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>Something shots follow and look at, such as the protagonist; a shot without a named target takes the first one.</summary>
public struct CameraTarget
{
    /// <summary>How far above the entity's origin cameras aim.</summary>
    public float Height { get; set; }
}
