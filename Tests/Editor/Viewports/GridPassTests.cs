using System.Numerics;

namespace FlickerVoid.Engine.Editor.Tests.Viewports;

public sealed class GridPassTests : IDisposable
{
    private const int Size = GridPassTestsBuilder.TargetSize;
    private const int Centre = Size / 2;

    private readonly GridPassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsTheXAxisRedAndTheZAxisBlueThroughTheOrigin()
    {
        // Looking straight down with +X to the right, the X axis runs across the middle row and the Z axis down the middle column.
        _builder.Camera.LookAt(Vector3.Zero, 0, -float.Pi / 2, 10);

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) onXAxis = GridPassTestsBuilder.Channels(pixels[(Centre * Size) + 5]);
        (byte red, byte green, byte blue) onZAxis = GridPassTestsBuilder.Channels(pixels[(5 * Size) + Centre]);
        onXAxis.red.Should().BeGreaterThan(100);
        onXAxis.blue.Should().BeLessThan(onXAxis.red);
        onZAxis.blue.Should().BeGreaterThan(100);
        onZAxis.red.Should().BeLessThan(onZAxis.blue);
    }

    [Fact]
    public void DrawsCellLinesEveryUnit()
    {
        _builder.Camera.LookAt(Vector3.Zero, 0, -float.Pi / 2, 10);
        float pixelsPerUnit = Size / _builder.Camera.ViewHeight;
        int lineColumn = Centre + (int)MathF.Round(pixelsPerUnit);
        int cellColumn = Centre + (int)MathF.Round(pixelsPerUnit / 2);

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) onLine = GridPassTestsBuilder.Channels(pixels[(5 * Size) + lineColumn]);
        (byte red, byte green, byte blue) inCell = GridPassTestsBuilder.Channels(pixels[(5 * Size) + cellColumn]);
        onLine.green.Should().BeGreaterThan(inCell.green);
    }

    [Fact]
    public void DrawsNothingAboveTheHorizon()
    {
        // Even the bottom of the 60 degree view looks upward.
        _builder.Camera.LookAt(new Vector3(0, 10, 0), 0, 0.6f, 1);

        uint[] pixels = _builder.Render();

        uint top = pixels[Centre];
        GridPassTestsBuilder.Channels(top).Should().Be(((byte)0, (byte)0, (byte)0));
    }
}
