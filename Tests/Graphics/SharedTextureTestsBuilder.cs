using FlickerVoid.Engine.Graphics.Vulkan;

namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>
/// Creates a Vulkan device like <see cref="GraphicsDeviceTestsBuilder"/> and a shared texture on it, skipping the test when
/// the driver can't export memory and semaphores (lavapipe exports no semaphores).
/// </summary>
internal sealed class SharedTextureTestsBuilder : IDisposable
{
    public const int Width = 8;
    public const int Height = 4;

    private readonly GraphicsDeviceTestsBuilder _deviceBuilder = new(GraphicsApi.Vulkan);

    public VulkanGraphicsDevice BuildDevice() => (VulkanGraphicsDevice)_deviceBuilder.Build();

    public static VulkanSharedTexture CreateSharedTexture(VulkanGraphicsDevice device)
    {
        TextureUsage usage = TextureUsage.RenderTarget | TextureUsage.CopySource;
        TextureDescription description = new(Width, Height, PixelFormat.Rgba8UNorm, usage) { Name = "Shared" };
        try
        {
            return device.CreateSharedTexture(description);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }
    }

    public void Dispose() => _deviceBuilder.Dispose();
}
