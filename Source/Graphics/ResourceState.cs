namespace FlickerVoid.Engine.Graphics;

/// <summary>What a resource is used for next; command lists move resources between states with explicit barriers.</summary>
public enum ResourceState : byte
{
    /// <summary>Contents don't matter: new resources start here, and moving out of it may discard them.</summary>
    Undefined,
    CopySource,
    CopyDestination,
    ShaderRead,
    RenderTarget,
    DepthWrite,
    DepthRead,
    IndexBuffer,

    /// <summary>A swapchain back buffer ready to be shown.</summary>
    Present,
}
