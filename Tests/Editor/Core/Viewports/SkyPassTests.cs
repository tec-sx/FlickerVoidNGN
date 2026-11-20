using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Editor.Core.Tests.Viewports;

public sealed class SkyPassTests : IDisposable
{
    private const int Size = SkyPassTestsBuilder.TargetSize;
    private const int Centre = Size / 2;
    private const int TopRow = 1;
    private const int BottomRow = Size - 2;

    private readonly SkyPassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void FillsTheEmptyViewLighterAboveTheHorizonThanBelow()
    {
        _builder.Camera.LookAt(new Vector3(0, 1, 0), 0, 0, 5);

        uint[] pixels = _builder.Render();

        int above = SkyPassTestsBuilder.Brightness(pixels[(TopRow * Size) + Centre]);
        int below = SkyPassTestsBuilder.Brightness(pixels[(BottomRow * Size) + Centre]);
        below.Should().BeGreaterThan(0);
        above.Should().BeGreaterThan(below);
    }

    [Fact]
    public void ShadesAnOrthographicViewFromTopToBottomToo()
    {
        _builder.Camera.Projection = CameraProjection.Orthographic;
        _builder.Camera.LookAt(Vector3.Zero, 0, -0.5f, 10);

        uint[] pixels = _builder.Render();

        int top = SkyPassTestsBuilder.Brightness(pixels[(TopRow * Size) + Centre]);
        int bottom = SkyPassTestsBuilder.Brightness(pixels[(BottomRow * Size) + Centre]);
        top.Should().BeGreaterThan(bottom);
    }

    [Fact]
    public void DrawsNothingWhileHidden()
    {
        _builder.Render();
        _builder.Sky!.Visible = false;

        uint[] pixels = _builder.Render();

        SkyPassTestsBuilder.Channels(pixels[Centre]).Should().Be(((byte)0, (byte)0, (byte)0));
    }
}
