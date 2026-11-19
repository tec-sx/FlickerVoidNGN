using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class SceneRendererTests : IDisposable
{
    private const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 } } }
        """;

    private const string Quad = """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""";

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
    public void LightsMeshesByThePointLightsInRange()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 1] }, "pointLight": { "intensity": 2, "range": 4 } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte corner, _, _) = SceneRendererTestsBuilder.Channels(pixels[0]);
        (byte centre, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        corner.Should().BeGreaterThan(0);
        centre.Should().BeGreaterThan(corner, "the light is closest to the centre of the quad");
    }

    [Fact]
    public void IgnoresPointLightsOutOfRange()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 1] }, "pointLight": { "intensity": 2, "range": 0.9 } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }

    [Fact]
    public void LightsOnlyInsideASpotLightsCone()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 1] }, "spotLight": { "intensity": 2, "range": 4, "innerConeAngle": 25, "outerConeAngle": 30 } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte centre, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        centre.Should().BeGreaterThan(0, "the centre of the quad is inside the cone");
        pixels[0].Should().Be(BlackPixel, "the corner of the quad is outside the cone");
    }

    [Fact]
    public void LightsMeshesThroughAPerspectiveCamera()
    {
        const string perspective = """
            { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "projection": "Perspective", "fieldOfView": 30 } } }
            """;
        _builder.WithModel("Quad.glb", static model => model.WithQuad(2).WithBaseColor(Red));
        _builder.WithScene(perspective, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 1] }, "pointLight": { "intensity": 2, "range": 4 } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        pixels.Should().NotContain(BlackPixel, "the light reaches the whole quad, which fills the view");
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
