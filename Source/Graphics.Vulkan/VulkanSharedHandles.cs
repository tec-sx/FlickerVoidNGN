namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>
/// OS handles to a <see cref="VulkanSharedTexture"/>'s memory and semaphores: opaque NT handles on Windows, opaque file descriptors
/// elsewhere. <see cref="MemorySize"/> is the size of the dedicated allocation the importer must make.
/// </summary>
public readonly record struct VulkanSharedHandles(nint Memory, long MemorySize, nint Ready, nint Released);
