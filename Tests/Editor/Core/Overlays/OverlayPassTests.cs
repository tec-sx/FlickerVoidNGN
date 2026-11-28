using System.Numerics;

using FlickerVoid.Editor.Core.Overlays;

namespace FlickerVoid.Editor.Core.Tests.Overlays;

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

    [Fact]
    public void AThinLineFadesAtItsEdgesAndCoversItsWidthInAll()
    {
        _builder.Camera.LookAt(Vector3.Zero, 0, 0, 10);
        _builder.WithOverlay(static overlay => overlay.AddLine(new Vector3(-2, 0.013f, 0), new Vector3(2, 0.013f, 0), Green, 1));

        uint[] pixels = _builder.Render();

        float covered = 0;
        int touched = 0;
        for (int row = 0; row < Size; row++)
        {
            (byte red, byte green, byte blue) pixel = OverlayPassTestsBuilder.Channels(pixels[(row * Size) + Centre]);
            covered += pixel.green / 255f;
            if (pixel.green > 0)
            {
                touched++;
            }
        }

        // A line a pixel wide between two rows is shared by them, never lost or doubled.
        covered.Should().BeInRange(0.7f, 1.3f);
        touched.Should().BeInRange(1, 3);
    }

    [Fact]
    public void ADashedLineLeavesGaps()
    {
        _builder.Camera.LookAt(Vector3.Zero, 0, 0, 10);
        _builder.WithOverlay(static overlay => overlay.AddDashedLine(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), Green, 3, 6, 4));

        uint[] pixels = _builder.Render();

        int drawn = 0;
        int clear = 0;
        int runs = 0;
        bool wasDrawn = false;
        for (int column = 0; column < Size; column++)
        {
            bool isDrawn = OverlayPassTestsBuilder.Channels(pixels[(Centre * Size) + column]).Green > 128;
            if (isDrawn && !wasDrawn)
            {
                runs++;
            }

            wasDrawn = isDrawn;
            if (isDrawn)
            {
                drawn++;
            }
            else
            {
                clear++;
            }
        }

        runs.Should().BeGreaterThan(1);
        drawn.Should().BeGreaterThan(clear / 4);
    }

    [Fact]
    public void AHatchedFillIsStripedOnTheScreen()
    {
        _builder.Camera.LookAt(Vector3.Zero, 0, 0, 10);
        _builder.WithOverlay(static overlay =>
        {
            Vector3[] corners = [new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, 2, 0)];
            overlay.AddFill(corners, Green, hatched: true);
        });

        uint[] pixels = _builder.Render();

        int strong = 0;
        int faint = 0;
        for (int column = Centre - 9; column < Centre + 9; column++)
        {
            byte green = OverlayPassTestsBuilder.Channels(pixels[(Centre * Size) + column]).Green;
            green.Should().BeGreaterThan(40, "the gaps between the stripes keep some of the colour");
            if (green > 230)
            {
                strong++;
            }
            else if (green < 190)
            {
                faint++;
            }
        }

        strong.Should().BeGreaterThan(3);
        faint.Should().BeGreaterThan(5);
    }

    [Fact]
    public void DrawsIconsInTheirColourWhereTheyAre()
    {
        Vector4 red = new(1, 0, 0, 1);
        _builder.Camera.LookAt(Vector3.Zero, 0, 0, 10);
        _builder.WithOverlay(overlay => overlay.AddIcon(Vector3.Zero, EditorIcon.PointLight, red));

        uint[] pixels = _builder.Render();

        // The middle of the bulb is solid; the icon is a few dozen pixels across, so the corners stay clear.
        (byte red, byte green, byte blue) middle = OverlayPassTestsBuilder.Channels(pixels[((Centre - 4) * Size) + Centre]);
        middle.red.Should().BeGreaterThan(200);
        middle.green.Should().BeLessThan(30);
        OverlayPassTestsBuilder.Channels(pixels[0]).Should().Be(((byte)0, (byte)0, (byte)0));
    }
}
