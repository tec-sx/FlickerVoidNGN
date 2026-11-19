using System.Text.Json;

using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

public sealed class MaterialPreviewTests : IDisposable
{
    private const int Size = 32;

    private readonly MaterialPreviewTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData(PreviewShape.Sphere)]
    [InlineData(PreviewShape.Cube)]
    public void ShowsTheMaterialOnTheShapeInTheMiddleOfTheImage(PreviewShape shape)
    {
        MaterialPreview preview = _builder.Build();
        MaterialFile glowing = JsonSerializer.Deserialize("""{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0] } }""", RenderingJsonContext.Default.MaterialFile)!;

        byte[] pixels = preview.Render(glowing, shape, yaw: 0.6f, Size);

        pixels.Should().HaveCount(Size * Size * 4);
        (byte red, byte green, _) = MaterialPreviewTestsBuilder.PixelAt(pixels, Size, Size / 2, Size / 2);
        (_, byte cornerGreen, _) = MaterialPreviewTestsBuilder.PixelAt(pixels, Size, 0, 0);
        green.Should().BeGreaterThan(100, "the shape glows green where it covers the middle");
        red.Should().BeLessThan(30);
        cornerGreen.Should().BeLessThan(30, "the shape leaves the corners empty");
    }

    [Fact]
    public void ShowsAnEditedMaterialOnTheNextRender()
    {
        MaterialPreview preview = _builder.Build();
        MaterialFile material = JsonSerializer.Deserialize("""{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0] } }""", RenderingJsonContext.Default.MaterialFile)!;
        preview.Render(material, PreviewShape.Sphere, yaw: 0, Size);

        using JsonDocument blue = JsonDocument.Parse("[0, 0, 1]");
        material.Parameters["emissive"] = blue.RootElement.Clone();
        byte[] pixels = preview.Render(material, PreviewShape.Sphere, yaw: 0, Size);

        (_, byte green, byte blueChannel) = MaterialPreviewTestsBuilder.PixelAt(pixels, Size, Size / 2, Size / 2);
        blueChannel.Should().BeGreaterThan(100);
        green.Should().BeLessThan(30);
    }
}
