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
    public void CastsSunShadows()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Blocker", "components": { "transform": { "position": [0.75, 0.25, 1], "scale": [0.25, 0.25, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        _builder.WithLight(sunDirection: new Vector3(-1, 0, -1), ambient: 0.25f);

        uint[] pixels = _builder.Render();

        (byte shadowed, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        (byte lit, _, _) = SceneRendererTestsBuilder.Channels(pixels[9]);
        shadowed.Should().BeInRange(62, 66, "the blocker hides the sun from that part of the floor, leaving only the ambient light");
        lit.Should().BeInRange(195, 203, "the sun lights the rest of the floor at 45 degrees");
    }

    [Theory]
    [InlineData("pointLight", "")]
    [InlineData("spotLight", """, "innerConeAngle": 50, "outerConeAngle": 60""")]
    public void CastsLocalLightShadows(string light, string cone)
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(
            Camera,
            Quad,
            """{ "name": "Blocker", "components": { "transform": { "position": [0.4, 0.4, 1], "scale": [0.2, 0.2, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""",
            $$"""{ "name": "Light", "components": { "transform": { "position": [0, 0, 2] }, "{{light}}": { "intensity": 4, "range": 5, "castsShadows": true{{cone}} } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        pixels[3].Should().Be(BlackPixel, "the blocker hides the light from the floor's top right corner");
        (byte opposite, _, _) = SceneRendererTestsBuilder.Channels(pixels[12]);
        opposite.Should().BeGreaterThan(0, "nothing hides the light from the floor's bottom left corner");
    }

    [Fact]
    public void ProjectsASpotLightsCookie()
    {
        uint white = SceneRendererTestsBuilder.Rgba(255, 255, 255, 255);
        _builder.WithTexture("Cookie.tga", 4, 1, [white, white, BlackPixel, BlackPixel]);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 2] }, "spotLight": { "intensity": 4, "range": 5, "innerConeAngle": 40, "outerConeAngle": 45, "cookie": "Cookie.tga" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte left, _, _) = SceneRendererTestsBuilder.Channels(pixels[4]);
        left.Should().BeGreaterThan(0, "the cookie's left half is white");
        pixels[7].Should().Be(BlackPixel, "the cookie's right half is black");
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
