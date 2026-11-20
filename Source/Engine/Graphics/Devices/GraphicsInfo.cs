namespace FlickerVoid.Engine.Graphics.Devices;

/// <summary>What a graphics device is and how much memory its resources hold, for debugging.</summary>
/// <param name="AdapterName">The GPU's name.</param>
/// <param name="ApiVersion">The graphics API version the device runs, such as 1.3.</param>
/// <param name="DeviceMemoryBytes">The size of the GPU's own memory; integrated GPUs report the memory they share.</param>
/// <param name="AllocatedBytes">The memory held by the buffers and textures created and not yet released.</param>
/// <param name="Allocations">How many memory allocations hold it.</param>
public readonly record struct GraphicsInfo(string AdapterName, string ApiVersion, long DeviceMemoryBytes, long AllocatedBytes, int Allocations);

