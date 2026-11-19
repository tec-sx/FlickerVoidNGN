using System.Numerics;

using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.PostProcessing;

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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MovesSkinnedMeshesWithTheirAnimatedJoints(float time)
    {
        _builder.WithModel("Walker.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red).WithMovingJoint(Vector3.Zero, new Vector3(1, 1, 0), 2));
        string walker = $$"""{ "name": "Walker", "components": { "transform": {}, "meshRenderer": { "mesh": "Walker.glb" }, "meshAnimator": { "time": {{time}} } } }""";
        _builder.WithScene(Camera, walker);

        uint[] pixels = _builder.Render();

        uint[] centered =
        [
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
            BlackPixel, RedPixel, RedPixel, BlackPixel,
            BlackPixel, RedPixel, RedPixel, BlackPixel,
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
        ];
        uint[] moved =
        [
            BlackPixel, BlackPixel, RedPixel, RedPixel,
            BlackPixel, BlackPixel, RedPixel, RedPixel,
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
            BlackPixel, BlackPixel, BlackPixel, BlackPixel,
        ];
        if (time == 0)
        {
            pixels.Should().Equal(centered);
        }
        else
        {
            pixels.Should().Equal(moved);
        }
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
    public void LeavesLocalLightsOutWhileTheyAreSwitchedOff()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 1] }, "pointLight": { "intensity": 2, "range": 4 } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.WithRenderer(static renderer => renderer.LocalLights = false);

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
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
    public void LightsMeshesInsideAnIrradianceVolumeByItsProbes()
    {
        IrradianceProbes probes = new IrradianceProbesTestsBuilder().WithEvenIrradiance(new Vector3(0.5f, 0.5f, 0.5f)).Build();
        _builder.WithProbes("Room.probes", probes);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Volume", "components": { "transform": {}, "irradianceVolume": { "size": [4, 4, 4], "probesX": 2, "probesY": 2, "probesZ": 2, "probes": "Room.probes" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(126, 129, "the probes give every surface half the light");
    }

    [Fact]
    public void LeavesProbesOutWhileTheyAreSwitchedOff()
    {
        IrradianceProbes probes = new IrradianceProbesTestsBuilder().WithEvenIrradiance(new Vector3(0.5f, 0.5f, 0.5f)).Build();
        _builder.WithProbes("Room.probes", probes);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Volume", "components": { "transform": {}, "irradianceVolume": { "size": [4, 4, 4], "probesX": 2, "probesY": 2, "probesZ": 2, "probes": "Room.probes" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.WithRenderer(static renderer => renderer.Probes = false);

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }

    [Fact]
    public void LeavesMeshesOutsideTheIrradianceVolumeToTheOtherLights()
    {
        IrradianceProbes probes = new IrradianceProbesTestsBuilder().WithEvenIrradiance(new Vector3(0.5f, 0.5f, 0.5f)).Build();
        _builder.WithProbes("Room.probes", probes);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Volume", "components": { "transform": { "position": [10, 0, 0] }, "irradianceVolume": { "size": [4, 4, 4], "probesX": 2, "probesY": 2, "probesZ": 2, "probes": "Room.probes" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }

    [Theory]
    [InlineData("Orthographic")]
    [InlineData("Perspective")]
    public void DimsSurfacesBehindFog(string projection)
    {
        string camera = $$"""{ "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "projection": "{{projection}}", "orthographicHeight": 2, "fieldOfView": 30 } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(3).WithBaseColor(Red));
        _builder.WithScene(camera, Quad);
        _builder.WithFog(density: 0.1f, color: Vector3.Zero);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(153, 159, "black fog lets exp(-0.1 * 4.9) of the light through over the 4.9 units from the near plane");
    }

    [Fact]
    public void LightsFogByTheAmbientLight()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(new Vector4(0, 0, 0, 1)));
        _builder.WithScene(Camera, Quad);
        _builder.WithFog(density: 0.1f, color: Vector3.One);

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(97, 103, "white fog scatters 1 - exp(-0.49) of the ambient light toward the camera");
        green.Should().Be(red);
        blue.Should().Be(red);
    }

    [Fact]
    public void LightsFogByPointLights()
    {
        const string light = """{ "name": "Light", "components": { "transform": { "position": [0, 0, 2] }, "pointLight": { "intensity": 4, "range": 3 } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(new Vector4(0, 0, 0, 1)));
        _builder.WithScene(Camera, Quad, light);
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 1);
        _builder.WithFog(density: 0.1f, color: Vector3.One);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeGreaterThan(103, "the light adds to the ambient light the fog scatters");
    }

    [Fact]
    public void ThinsFogAboveItsHeight()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.WithFog(density: 0.1f, color: Vector3.Zero, height: 0, heightFalloff: 100);

        uint[] pixels = _builder.Render();

        pixels[1].Should().Be(RedPixel, "the fog has all but gone a quarter unit above its height");
        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[13]);
        red.Should().BeInRange(153, 159, "below its height the fog keeps its full density");
    }

    [Fact]
    public void LeavesSurfacesClearWithoutFog()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.WithFog(density: 0, color: Vector3.One);

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(RedPixel);
    }

    [Fact]
    public void DarkensAmbientLightNearOccluders()
    {
        _builder.WithTargetSize(16).WithMood(static mood => mood with { AmbientOcclusion = 1, AmbientOcclusionRadius = 0.5f });
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Step", "components": { "transform": { "position": [-0.5, 0, 0.3], "scale": [0.5, 1, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        (byte nearTheStep, _, _) = SceneRendererTestsBuilder.Channels(pixels[(8 * 16) + 8]);
        (byte open, _, _) = SceneRendererTestsBuilder.Channels(pixels[(8 * 16) + 15]);
        open.Should().Be(255, "nothing stands within the radius of the wall's far side");
        nearTheStep.Should().BeLessThan(235, "the step in front of the wall hides part of the sky from the wall beside it");
    }

    [Fact]
    public void CastsContactShadowsTheShadowMapMisses()
    {
        _builder.WithTargetSize(16).WithMood(static mood => mood with { SunShadowDistance = 0, ContactShadowLength = 0.3f });
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Blocker", "components": { "transform": { "position": [0.5, 0, 0.15], "scale": [0.25, 0.25, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        _builder.WithLight(sunDirection: new Vector3(-1, 0, -1), ambient: 0.25f);

        uint[] pixels = _builder.Render();

        (byte shadowed, _, _) = SceneRendererTestsBuilder.Channels(pixels[(7 * 16) + 9]);
        (byte lit, _, _) = SceneRendererTestsBuilder.Channels(pixels[(7 * 16) + 3]);
        shadowed.Should().BeInRange(62, 66, "the blocker just in front of the wall hides the sun from the strip beside it");
        lit.Should().BeInRange(195, 203);
    }

    [Fact]
    public void SpreadsBrightLightIntoBloom()
    {
        const string light = """{ "name": "Light", "components": { "transform": { "position": [0, 0, 0.5] }, "pointLight": { "intensity": 40, "range": 2 } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, light);
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        uint[] withoutBloom = _builder.Render();

        _builder.WithMood(static mood => mood with { Bloom = 0.3f });
        uint[] withBloom = _builder.Render();

        withoutBloom[0].Should().Be(BlackPixel);
        (byte glow, _, _) = SceneRendererTestsBuilder.Channels(withBloom[0]);
        glow.Should().BeGreaterThan(0, "the bright quad glows onto the dark corner");
    }

    [Fact]
    public void GradesTheImage()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);

        _builder.WithMood(static mood => mood with { Exposure = 0.5f });
        (byte exposed, _, _) = SceneRendererTestsBuilder.Channels(_builder.Render()[5]);
        _builder.WithMood(static mood => mood with { Exposure = 1, Saturation = 0 });
        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(_builder.Render()[5]);
        _builder.WithMood(static mood => mood with { Saturation = 1, Tint = new Vector3(0.5f, 1, 1) });
        (byte tinted, _, _) = SceneRendererTestsBuilder.Channels(_builder.Render()[5]);

        exposed.Should().BeInRange(126, 129);
        red.Should().BeInRange(53, 55, "a grey red keeps red's luminance, 0.2126");
        green.Should().Be(red);
        blue.Should().Be(red);
        tinted.Should().BeInRange(126, 129);
    }

    [Fact]
    public void LaysGrainAndPaperOverTheImage()
    {
        uint grey = SceneRendererTestsBuilder.Rgba(128, 128, 128, 255);
        _builder.WithTexture("Paper.tga", 1, 1, [grey]);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);

        _builder.WithMood(static mood => mood with { Paper = "Paper.tga" });
        uint[] paper = _builder.Render();
        _builder.WithMood(static mood => mood with { Paper = null, Grain = 0.2f });
        uint[] grain = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(paper[5]);
        red.Should().BeInRange(53, 57, "paper at sRGB 128 lets about a fifth of the light through");
        grain.Distinct().Should().HaveCountGreaterThan(1, "grain varies from pixel to pixel");
    }

    [Fact]
    public void OnlyClearsWithoutACamera()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene("""{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }

    [Fact]
    public void RedrawsAModelChangedOnDisk()
    {
        Vector4 blue = new(0, 0, 1, 1);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.Render();

        _builder.WithModel("Quad.glb", model => model.WithQuad(1).WithBaseColor(blue));
        _builder.NotifyChanged("Quad.model");
        uint[] pixels = _builder.Render();

        uint bluePixel = SceneRendererTestsBuilder.Rgba(0, 0, 255, 255);
        pixels.Should().AllBeEquivalentTo(bluePixel);
    }

    [Fact]
    public void RedrawsATextureChangedOnDisk()
    {
        uint white = SceneRendererTestsBuilder.Rgba(255, 255, 255, 255);
        _builder.WithTexture("Cookie.tga", 4, 1, [white, white, BlackPixel, BlackPixel]);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Light", "components": { "transform": { "position": [0, 0, 2] }, "spotLight": { "intensity": 4, "range": 5, "innerConeAngle": 40, "outerConeAngle": 45, "cookie": "Cookie.tga" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.Render();

        _builder.WithTexture("Cookie.tga", 4, 1, [BlackPixel, BlackPixel, white, white]);
        _builder.NotifyChanged("Cookie.dds");
        uint[] pixels = _builder.Render();

        pixels[4].Should().Be(BlackPixel, "the edited cookie's left half is black");
        (byte right, _, _) = SceneRendererTestsBuilder.Channels(pixels[7]);
        right.Should().BeGreaterThan(0, "the edited cookie's right half is white");
    }

    [Fact]
    public void DrawsMeshPartsWithTheMaterialsTheirRendererNames()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Glow.material.json", """{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0] } }""");
        _builder.WithScene(Camera, MaterialQuad("Glow.material.json"));
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        uint green = SceneRendererTestsBuilder.Rgba(0, 255, 0, 255);
        pixels.Should().AllBeEquivalentTo(green, "a black tint hides the red base color and the emissive light shows alone");
    }

    [Fact]
    public void WrapsDiffuseLightRoundToSurfacesTheLightGrazes()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Wrapped.material.json", """{ "parameters": { "wrap": 1 } }""");
        _builder.WithScene(Camera, MaterialQuad("Wrapped.material.json"));
        _builder.WithLight(sunDirection: -Vector3.UnitX, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(124, 132, "full wrap lights a surface the light grazes by half");
    }

    [Fact]
    public void TintsTheSidesTurnedFromTheLight()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Vector4.One));
        _builder.WithFile("Cool.material.json", """{ "parameters": { "shadowTint": [0, 0, 0.5] } }""");
        _builder.WithScene(Camera, MaterialQuad("Cool.material.json"));
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte red, _, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().Be(0);
        blue.Should().BeInRange(124, 132, "unlit sides take the shadow tint");
    }

    [Fact]
    public void AddsASpecularHighlightWhereTheLightReflectsTowardTheCamera()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Shiny.material.json", """{ "parameters": { "specular": 0.5 } }""");
        _builder.WithScene(Camera, MaterialQuad("Shiny.material.json"));
        _builder.WithLight(sunDirection: -Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (_, byte green, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        green.Should().BeInRange(124, 132, "a mirror-facing surface reflects the specular share of white sunlight");
    }

    [Fact]
    public void KeepsTheModelsLookWithoutAMaterial()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Plain.material.json", """{ "parameters": {} }""");
        _builder.WithScene(Camera, MaterialQuad("Plain.material.json"));

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(RedPixel, "every parameter defaults to the plain look");
    }

    [Fact]
    public void RedrawsAMaterialChangedOnDisk()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Glow.material.json", """{ "parameters": { "emissive": [0, 0, 0] } }""");
        _builder.WithScene(Camera, MaterialQuad("Glow.material.json"));
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.Render();

        _builder.WithFile("Glow.material.json", """{ "parameters": { "emissive": [0, 0, 1] } }""");
        _builder.NotifyChanged("Glow.material.json");
        uint[] pixels = _builder.Render();

        uint blue = SceneRendererTestsBuilder.Rgba(0, 0, 255, 255);
        pixels.Should().AllBeEquivalentTo(blue);
    }

    private static string MaterialQuad(string material)
    {
        return $$"""{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb", "materials": ["{{material}}"] } } }""";
    }

    [Theory]
    [InlineData(Tonemapper.Uncharted2, 1f, 126)]
    [InlineData(Tonemapper.Uncharted2, 4f, 234)]
    [InlineData(Tonemapper.Aces, 1f, 205)]
    [InlineData(Tonemapper.None, 4f, 255)]
    public void BringsLightIntoTheScreensRangeByTheMoodsTonemapper(Tonemapper tonemapper, float exposure, byte expectedRed)
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.WithMood(mood => mood with { Tonemapper = tonemapper, Exposure = exposure });

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange((byte)(expectedRed - 2), (byte)Math.Min(expectedRed + 2, 255));
    }
}
