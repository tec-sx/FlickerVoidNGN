using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class SceneRendererTests : IDisposable
{
    private const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 } } }
        """;

    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly uint RedPixel = SceneRendererTestsBuilder.Rgba(255, 0, 0, 255);
    private static readonly uint BlackPixel = SceneRendererTestsBuilder.Rgba(0, 0, 0, 255);

    private readonly SceneRendererTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsMeshesInTheirBaseColorWhereTheyCover()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        uint[] expected =
        [
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
            BlackPixel, RedPixel, RedPixel, BlackPixel,
            BlackPixel, RedPixel, RedPixel, BlackPixel,
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
        ];
        pixels.Should().Equal(expected);
    }

    [Fact]
    public void PlacesMeshesByTheirTransform()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": { "position": [0.5, 0.5, 0] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        uint[] expected =
        [
            BlackPixel, BlackPixel, RedPixel, RedPixel,
            BlackPixel, BlackPixel, RedPixel, RedPixel,
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
        ];
        pixels.Should().Equal(expected);
    }

    [Fact]
    public void CullsMeshesFacingAway()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": { "rotation": [0, 1, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }

    [Fact]
    public void SamplesTheBaseColorTexture()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithRedBlueTexture());
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        (byte red, _, byte blue) = SceneRendererTestsBuilder.Channels(pixels[0]);
        red.Should().BeGreaterThan(blue, "the left of the quad samples the red texel");
        (red, _, blue) = SceneRendererTestsBuilder.Channels(pixels[3]);
        blue.Should().BeGreaterThan(red, "the right of the quad samples the blue texel");
    }

    [Fact]
    public void LightsMeshesByTheSunAndTheAmbientLight()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        _builder.WithLight(sunDirection: -Vector3.UnitZ, ambient: 0.25f);

        uint[] lit = _builder.Render();
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0.25f);
        uint[] unlit = _builder.Render();

        lit.Should().AllBeEquivalentTo(RedPixel);
        (byte red, _, _) = SceneRendererTestsBuilder.Channels(unlit[0]);
        red.Should().BeInRange(62, 66, "a face turned from the sun gets only the ambient light");
    }

    [Fact]
    public void OnlyClearsWithoutACamera()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene("""{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }
}
