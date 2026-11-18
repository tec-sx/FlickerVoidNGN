using System.Numerics;

namespace FlickerVoid.Engine.Graphics.Tests;

public sealed class SwapchainTests : IDisposable
{
    private SwapchainTestsBuilder? _builder;

    public static TheoryData<GraphicsApi> Apis => GraphicsDeviceTestsBuilder.Apis;

    public void Dispose() => _builder?.Dispose();

    [Theory]
    [MemberData(nameof(Apis))]
    public void RendersAndPresentsMoreFramesThanItHasBackBuffers(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);
        Vector4[] colors = [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1), new(1, 1, 1, 1), new(0, 0, 0, 1)];

        foreach (Vector4 color in colors)
        {
            uint[] pixels = _builder.RenderFrame(device, swapchain, color);

            uint expected = SwapchainTestsBuilder.Bgra(color);
            pixels.Should().HaveCount(SwapchainTestsBuilder.WindowWidth * SwapchainTestsBuilder.WindowHeight).And.AllBeEquivalentTo(expected);
        }
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void DescribesItsBackBuffers(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);

        ITexture backBuffer = swapchain.AcquireNextTexture();

        swapchain.Width.Should().Be(SwapchainTestsBuilder.WindowWidth);
        swapchain.Height.Should().Be(SwapchainTestsBuilder.WindowHeight);
        swapchain.Format.Should().Be(PixelFormat.Bgra8UNorm);
        TextureDescription expected = new(swapchain.Width, swapchain.Height, swapchain.Format, TextureUsage.RenderTarget | TextureUsage.CopySource);
        backBuffer.Description.Should().BeEquivalentTo(expected, static options => options.Excluding(static description => description.Name));
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void ResizesItsBackBuffersWithTheWindow(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);
        _builder.RenderFrame(device, swapchain, Vector4.One);

        _builder.ResizeWindow(48, 16);
        swapchain.Resize(48, 16);
        uint[] pixels = _builder.RenderFrame(device, swapchain, Vector4.One);

        swapchain.Width.Should().Be(48);
        swapchain.Height.Should().Be(16);
        pixels.Should().HaveCount(48 * 16);
    }

    [Theory]
    [MemberData(nameof(Apis))]
    public void RefusesToPresentABackBufferThatIsNotInThePresentState(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
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

    [Theory]
    [MemberData(nameof(Apis))]
    public void RefusesToPresentBeforeABackBufferIsAcquired(GraphicsApi api)
    {
        using IGraphicsDevice device = CreateDevice(api);
        using ISwapchain swapchain = _builder!.CreateSwapchain(device);

        Action present = swapchain.Present;

        present.Should().Throw<InvalidOperationException>();
    }

    private IGraphicsDevice CreateDevice(GraphicsApi api)
    {
        _builder = new SwapchainTestsBuilder(api);
        return _builder.Build();
    }
}
