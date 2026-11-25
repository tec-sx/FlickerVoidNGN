namespace FlickerVoid.Engine.Graphics.Devices;

/// <summary>What a device can do beyond what every Vulkan 1.3 GPU does.</summary>
[Flags]
public enum GraphicsFeatures
{
    None = 0,

    /// <summary>Shaders trace rays against acceleration structures (ray queries), as on RTX and other ray tracing GPUs.</summary>
    RayQueries = 1,

    /// <summary>NVIDIA's DLSS upscales frames (<see cref="IGraphicsDevice.CreateUpscaler"/>): an RTX GPU, with the engine's DLSS library beside the program.</summary>
    Dlss = 2,
}
