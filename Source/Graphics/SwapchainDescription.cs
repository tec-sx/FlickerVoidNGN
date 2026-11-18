using Silk.NET.Core.Contexts;

namespace FlickerVoid.Engine.Graphics;

/// <summary>The window a swapchain presents to and the size of its back buffers.</summary>
public readonly record struct SwapchainDescription(INativeWindow Window, int Width, int Height)
{
    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
