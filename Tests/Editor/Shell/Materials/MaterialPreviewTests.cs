using System.Text.Json;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Assets.Tests.Materials;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Tests.Materials;

public sealed class MaterialPreviewTests : IDisposable
{
    private const int Size = 32;

    private readonly MaterialPreviewTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData(PreviewShape.Sphere)]
    [InlineData(PreviewShape.Cube)]
    [InlineData(PreviewShape.Plane)]
    public void ShowsTheMaterialOnTheShapeInTheMiddleOfTheImage(PreviewShape shape)
    {
        MaterialPreview preview = _builder.Build();
        MaterialFile glowing = JsonSerializer.Deserialize("""{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0] } }""", RenderingJsonContext.Default.MaterialFile)!;

        byte[] pixels = preview.Render(glowing, new PreviewView(shape, Yaw: 0.6f), Size);

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
        preview.Render(material, new PreviewView(PreviewShape.Sphere, Yaw: 0), Size);

        using JsonDocument blue = JsonDocument.Parse("[0, 0, 1]");
        material.Parameters["emissive"] = blue.RootElement.Clone();
        byte[] pixels = preview.Render(material, new PreviewView(PreviewShape.Sphere, Yaw: 0), Size);

        (_, byte green, byte blueChannel) = MaterialPreviewTestsBuilder.PixelAt(pixels, Size, Size / 2, Size / 2);
        blueChannel.Should().BeGreaterThan(100);
        green.Should().BeLessThan(30);
    }

    [Fact]
    public void ShowsTexturesFromTheEditorsContent()
    {
        MaterialPreview preview = _builder.WithTexture("Green.png", 0, 255, 0).Build();
        MaterialFile material = JsonSerializer.Deserialize("""{ "parameters": { "texture": "Green.png" } }""", RenderingJsonContext.Default.MaterialFile)!;

        byte[] pixels = preview.Render(material, new PreviewView(PreviewShape.Sphere, Yaw: 0), Size);

        (byte red, byte green, _) = MaterialPreviewTestsBuilder.PixelAt(pixels, Size, Size / 2, Size / 2);
        green.Should().BeGreaterThan(red, "the texture comes from the editor's content folder, not the preview's own");
    }

    [Theory]
    [InlineData(PreviewMood.Night)]
    [InlineData(PreviewMood.Indoors)]
    public void LightsTheShapeDimmerOutOfTheDay(PreviewMood mood)
    {
        MaterialPreview preview = _builder.Build();
        MaterialFile white = JsonSerializer.Deserialize("""{ "parameters": {} }""", RenderingJsonContext.Default.MaterialFile)!;
        byte[] day = preview.Render(white, new PreviewView(PreviewShape.Sphere, Yaw: 0), Size);

        byte[] dim = preview.Render(white, new PreviewView(PreviewShape.Sphere, Yaw: 0, mood), Size);

        (byte dayRed, _, _) = MaterialPreviewTestsBuilder.PixelAt(day, Size, Size / 2, Size / 2);
        (byte dimRed, _, _) = MaterialPreviewTestsBuilder.PixelAt(dim, Size, Size / 2, Size / 2);
        dimRed.Should().BeLessThan(dayRed);
        dimRed.Should().BeGreaterThan(0, "a lamp still lights the shape");
    }

    [Fact]
    public void TurnsTheLightAboutTheShape()
    {
        MaterialPreview preview = _builder.Build();
        MaterialFile white = JsonSerializer.Deserialize("""{ "parameters": {} }""", RenderingJsonContext.Default.MaterialFile)!;
        byte[] before = preview.Render(white, new PreviewView(PreviewShape.Sphere, Yaw: 0), Size);

        byte[] turned = preview.Render(white, new PreviewView(PreviewShape.Sphere, Yaw: 0, LightYaw: MathF.PI), Size);

        turned.Should().NotEqual(before);
    }
}
