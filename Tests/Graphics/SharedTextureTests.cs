using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;

namespace FlickerVoid.Engine.Graphics.Tests;

public sealed class SharedTextureTests : IDisposable
{
    private readonly SharedTextureTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ExportsItsMemoryAndSemaphoresAsHandles()
    {
        using VulkanGraphicsDevice device = _builder.BuildDevice();
        using VulkanSharedTexture shared = SharedTextureTestsBuilder.CreateSharedTexture(device);

        VulkanSharedHandles handles = shared.Export();

        ((long)handles.Memory).Should().BePositive();
        ((long)handles.Ready).Should().BePositive();
        ((long)handles.Released).Should().BePositive();
        handles.MemorySize.Should().BeGreaterThanOrEqualTo(SharedTextureTestsBuilder.Width * SharedTextureTestsBuilder.Height * 4);
    }

    [Fact]
    public void RendersAFrameBetweenBeginAndEnd()
    {
        using VulkanGraphicsDevice device = _builder.BuildDevice();
        using VulkanSharedTexture shared = SharedTextureTestsBuilder.CreateSharedTexture(device);
        Vector4 color = new(0, 1, 0, 1);

        shared.BeginFrame();
        using (ICommandList commands = device.CreateCommandList())
        {
            commands.Begin();
            commands.Barrier(shared.Texture, ResourceState.RenderTarget);
            commands.BeginRendering([new ColorTarget(shared.Texture, LoadAction.Clear, color)]);
            commands.EndRendering();
            commands.End();
            device.Submit(commands);
        }

        uint[] pixels = GraphicsDeviceTestsBuilder.ReadPixels(device, shared.Texture);
        shared.EndFrame();
        device.WaitIdle();

        uint green = GraphicsDeviceTestsBuilder.Rgba(0, 255, 0, 255);
        pixels.Should().AllBeEquivalentTo(green);
    }

    [Fact]
    public void RefusesToEndAFrameItHasNotBegun()
    {
        using VulkanGraphicsDevice device = _builder.BuildDevice();
        using VulkanSharedTexture shared = SharedTextureTestsBuilder.CreateSharedTexture(device);

        Action end = shared.EndFrame;

        end.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RefusesToEndAFrameUnlessTheTextureIsInTheCopySourceState()
    {
        using VulkanGraphicsDevice device = _builder.BuildDevice();
        using VulkanSharedTexture shared = SharedTextureTestsBuilder.CreateSharedTexture(device);
        shared.BeginFrame();
        using ICommandList commands = device.CreateCommandList();
        commands.Begin();
        commands.Barrier(shared.Texture, ResourceState.RenderTarget);
        commands.End();
        device.Submit(commands);

        Action end = shared.EndFrame;

        end.Should().Throw<InvalidOperationException>();
        device.WaitIdle();
    }
}
