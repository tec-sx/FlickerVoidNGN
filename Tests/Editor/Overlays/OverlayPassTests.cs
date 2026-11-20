using System.Numerics;

namespace FlickerVoid.Engine.Editor.Tests.Overlays;

public sealed class OverlayPassTests : IDisposable
{
    private const int Size = OverlayPassTestsBuilder.TargetSize;
    private const int Centre = Size / 2;

    private static readonly Vector4 Green = new(0, 1, 0, 1);

    private readonly OverlayPassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsItsTrianglesInTheirColour()
    {
        _builder.Camera.LookAt(Vector3.Zero, 0, 0, 10);
        _builder.WithOverlay(static overlay => overlay.AddTriangle(new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(0, 1, 0), Green));

        uint[] pixels = _builder.Render();

        OverlayPassTestsBuilder.Channels(pixels[(Centre * Size) + Centre]).Should().Be(((byte)0, (byte)255, (byte)0));
        OverlayPassTestsBuilder.Channels(pixels[0]).Should().Be(((byte)0, (byte)0, (byte)0));
    }

    [Fact]
    public void DrawsLinesAsWideAsAsked()
    {
        _builder.Camera.LookAt(Vector3.Zero, 0, 0, 10);
        _builder.WithOverlay(static overlay => overlay.AddLine(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), Green, 5));

        uint[] pixels = _builder.Render();

        int covered = 0;
        for (int row = 0; row < Size; row++)
        {
            (byte red, byte green, byte blue) pixel = OverlayPassTestsBuilder.Channels(pixels[(row * Size) + Centre]);
            if (pixel.green > 128)
            {
                covered++;
            }
        }

        covered.Should().BeInRange(4, 6);
    }
}
