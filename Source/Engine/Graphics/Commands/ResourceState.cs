namespace FlickerVoid.Engine.Graphics.Commands;

/// <summary>What a resource is used for next; command lists move resources between states with explicit barriers.</summary>
public enum ResourceState : byte
{
    /// <summary>Contents don't matter: new resources start here, and moving out of it may discard them.</summary>
    Undefined,
    CopySource,
    CopyDestination,
    ShaderRead,

    /// <summary>Shaders read and write it; barriers between two writing passes need it twice, once per pass.</summary>
    ShaderWrite,
    RenderTarget,
    DepthWrite,
    DepthRead,
    IndexBuffer,

    /// <summary>A swapchain back buffer ready to be shown.</summary>
    Present,
}

