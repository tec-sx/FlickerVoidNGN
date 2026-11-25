using System.Numerics;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Upscaling;

namespace FlickerVoid.Engine.Graphics.Tests.Upscaling;

public sealed class UpscalerTests : IDisposable
{
    private readonly UpscalerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void RefusesToUpscaleOnADeviceWithoutDlss()
    {
        IGraphicsDevice device = _builder.WithoutDlss().Build();
        UpscalerDescription description = new(UpscaleMode.Quality, 64, 64);

        Action create = () => device.CreateUpscaler(description);

        device.Features.Should().NotHaveFlag(GraphicsFeatures.Dlss);
        create.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DrawsTheSceneAtTwoThirdsOfTheOutputsSizeForQuality()
    {
        IUpscaler upscaler = _builder.CreateUpscaler(UpscaleMode.Quality, 1920, 1080);

        upscaler.RenderWidth.Should().BeCloseTo(1280, 1);
        upscaler.RenderHeight.Should().BeCloseTo(720, 1);
    }

    [Fact]
    public void DrawsTheSceneAtTheOutputsSizeForDlaa()
    {
        IUpscaler upscaler = _builder.CreateUpscaler(UpscaleMode.Dlaa, 1920, 1080);

        upscaler.RenderWidth.Should().Be(1920);
        upscaler.RenderHeight.Should().Be(1080);
    }

    [Fact]
    public void RefusesToMakeAnUpscalerThatUpscalesNothing()
    {
        IGraphicsDevice device = _builder.Build();
        UpscalerDescription description = new(UpscaleMode.Off, 64, 64);

        Action create = () => device.CreateUpscaler(description);

        create.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(UpscaleMode.Quality)]
    [InlineData(UpscaleMode.Dlaa)]
    public void FillsTheOutputWithAFlatFramesColor(UpscaleMode mode)
    {
        IUpscaler upscaler = _builder.CreateUpscaler(mode, 640, 360);
        Vector4 color = new(0.5f, 0.25f, 0.125f, 1);

        Vector4[] output = _builder.UpscaleFlat(upscaler, color);

        output.Should().HaveCount(640 * 360);
        output.Should().AllSatisfy(pixel =>
        {
            pixel.X.Should().BeApproximately(color.X, 0.05f);
            pixel.Y.Should().BeApproximately(color.Y, 0.05f);
            pixel.Z.Should().BeApproximately(color.Z, 0.05f);
        });
    }
}
