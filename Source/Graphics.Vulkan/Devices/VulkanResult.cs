using Silk.NET.Vulkan;

namespace FlickerVoid.Engine.Graphics.Vulkan.Devices;

/// <summary>Turns failed Vulkan results into exceptions.</summary>
internal static class VulkanResult
{
    public static void Check(Result result)
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Vulkan call failed: {result}.");
        }
    }
}

