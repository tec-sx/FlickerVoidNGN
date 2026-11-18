using System.Numerics;

namespace FlickerVoid.Engine.Graphics.Tests;

public sealed class SwapchainTests : IDisposable
{
    private SwapchainTestsBuilder? _builder;

    public void Dispose() => _builder?.Dispose();

    [Fact]
    public void RendersAndPresentsMoreFramesThanItHasBackBuffers()
    {
        using IGraphicsDevice device = CreateDevice();
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);
        Vector4[] colors = [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1), new(1, 1, 1, 1), new(0, 0, 0, 1)];

        foreach (Vector4 color in colors)
        {
            uint[] pixels = _builder.RenderFrame(device, swapchain, color);

            uint expected = SwapchainTestsBuilder.Bgra(color);
            pixels.Should().HaveCount(SwapchainTestsBuilder.WindowWidth * SwapchainTestsBuilder.WindowHeight).And.AllBeEquivalentTo(expected);
        }
    }

    [Fact]
    public void DescribesItsBackBuffers()
    {
        using IGraphicsDevice device = CreateDevice();
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);

        ITexture backBuffer = swapchain.AcquireNextTexture();

        swapchain.Width.Should().Be(SwapchainTestsBuilder.WindowWidth);
        swapchain.Height.Should().Be(SwapchainTestsBuilder.WindowHeight);
        swapchain.Format.Should().Be(PixelFormat.Bgra8UNorm);
        TextureDescription expected = new(swapchain.Width, swapchain.Height, swapchain.Format, TextureUsage.RenderTarget | TextureUsage.CopySource);
        backBuffer.Description.Should().BeEquivalentTo(expected, static options => options.Excluding(static description => description.Name));
    }

    [Fact]
    public void ResizesItsBackBuffersWithTheWindow()
    {
        using IGraphicsDevice device = CreateDevice();
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);
        _builder.RenderFrame(device, swapchain, Vector4.One);

        _builder.ResizeWindow(48, 16);
        swapchain.Resize(48, 16);
        uint[] pixels = _builder.RenderFrame(device, swapchain, Vector4.One);

        swapchain.Width.Should().Be(48);
        swapchain.Height.Should().Be(16);
        pixels.Should().HaveCount(48 * 16);
    }

    [Fact]
    public void RefusesToPresentABackBufferThatIsNotInThePresentState()
    {
        using IGraphicsDevice device = CreateDevice();
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);
        using ICommandList commands = device.CreateCommandList();
        ITexture backBuffer = swapchain.AcquireNextTexture();
        commands.Begin();
        commands.Barrier(backBuffer, ResourceState.RenderTarget);
        commands.End();
        device.Submit(commands);

        Action present = swapchain.Present;

        present.Should().Throw<InvalidOperationException>();
        device.WaitIdle();
    }

    [Fact]
    public void RefusesToPresentBeforeABackBufferIsAcquired()
    {
        using IGraphicsDevice device = CreateDevice();
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);

        Action present = swapchain.Present;

        present.Should().Throw<InvalidOperationException>();
    }

    private IGraphicsDevice CreateDevice()
    {
        _builder = new SwapchainTestsBuilder();
        return _builder.Build();
    }
}
