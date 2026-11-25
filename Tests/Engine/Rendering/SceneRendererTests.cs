using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class SceneRendererTests : IDisposable
{
    private const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 } } }
        """;

    private const string Quad = """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" } } }""";

    /// <summary>A part a module's mesh source makes, standing two metres below the view's centre, so a box four high covers the view.</summary>
    private const string VanBody = """{ "name": "Van", "components": { "transform": { "position": [0, -2, 0] }, "meshRenderer": { "mesh": "Van.fvtest#body" } } }""";

    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly uint RedPixel = SceneRendererTestsBuilder.Rgba(255, 0, 0, 255);
    private static readonly uint BlackPixel = SceneRendererTestsBuilder.Rgba(0, 0, 0, 255);

    /// <summary>Where nothing was drawn: transparent, so a stage's image shows over the interface there.</summary>
    private static readonly uint EmptyPixel = SceneRendererTestsBuilder.Rgba(0, 0, 0, 0);

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
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
            EmptyPixel, RedPixel, RedPixel, EmptyPixel,
            EmptyPixel, RedPixel, RedPixel, EmptyPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
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
            EmptyPixel, EmptyPixel, RedPixel, RedPixel,
            EmptyPixel, EmptyPixel, RedPixel, RedPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
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
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
            EmptyPixel, RedPixel, RedPixel, EmptyPixel,
            EmptyPixel, RedPixel, RedPixel, EmptyPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
        ];
        uint[] moved =
        [
            EmptyPixel, EmptyPixel, RedPixel, RedPixel,
            EmptyPixel, EmptyPixel, RedPixel, RedPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
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
    public void PosesSkinnedMeshesAnewInEveryFrame()
    {
        _builder.WithModel("Walker.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red).WithMovingJoint(Vector3.Zero, new Vector3(1, 1, 0), 2));
        _builder.WithScene(Camera, """{ "name": "Walker", "components": { "transform": {}, "meshRenderer": { "mesh": "Walker.glb" }, "meshAnimator": { "time": 0 } } }""");
        _builder.Render();

        uint[] moved = _builder.WithAnimationTime(1).Render();
        uint[] back = _builder.WithAnimationTime(0).Render();

        uint[] movedWalker =
        [
            EmptyPixel, EmptyPixel, RedPixel, RedPixel,
            EmptyPixel, EmptyPixel, RedPixel, RedPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
        ];
        uint[] centeredWalker =
        [
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
            EmptyPixel, RedPixel, RedPixel, EmptyPixel,
            EmptyPixel, RedPixel, RedPixel, EmptyPixel,
            EmptyPixel, EmptyPixel, EmptyPixel, EmptyPixel,
        ];
        moved.Should().Equal(movedWalker);
        back.Should().Equal(centeredWalker);
    }

    [Fact]
    public void CullsMeshesFacingAway()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": { "rotation": [0, 1, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(EmptyPixel);
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

    [Fact]
    public void CullsMeshesOutsideTheViewAndOutsideEachShadowView()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(
            Camera,
            Quad,
            """{ "name": "Blocker", "components": { "transform": { "position": [2, 0, 2], "scale": [0.25, 0.25, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""",
            """{ "name": "Far", "components": { "transform": { "position": [100, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        _builder.WithLight(sunDirection: new Vector3(-1, 0, -1), ambient: 0.25f);

        _builder.Render();

        RenderStatistics statistics = _builder.Statistics;
        statistics.Drawn.Should().Be(1, "only the floor is in view");
        statistics.Culled.Should().Be(2);
        statistics.ShadowDrawn.Should().Be(2, "the blocker outside the view stands between the floor and the sun, so it casts onto it");
        statistics.ShadowCulled.Should().Be(1, "the far quad is outside the sun's view too");
    }

    [Fact]
    public void ShadowOnlyMeshesCastShadowsUnseen()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Blocker", "components": { "transform": { "position": [0.75, 0.25, 1], "scale": [0.25, 0.25, 1] }, "meshRenderer": { "mesh": "Quad.glb" } } }""");
        _builder.WithComponent("Blocker", new ShadowOnly());
        _builder.WithLight(sunDirection: new Vector3(-1, 0, -1), ambient: 0.25f);

        uint[] pixels = _builder.Render();

        (byte shadowed, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        (byte behind, _, _) = SceneRendererTestsBuilder.Channels(pixels[7]);
        shadowed.Should().BeInRange(62, 66, "the unseen blocker still hides the sun from that part of the floor");
        behind.Should().BeInRange(195, 203, "the floor shows where the blocker stands");
    }

    [Fact]
    public void DrawsGreyedMeshesInBlackAndWhite()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.WithComponent("Quad", new Greyed());

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeGreaterThan(0, "the quad is still drawn");
        green.Should().Be(red, "a greyed mesh has no colour");
        blue.Should().Be(red);
    }

    [Fact]
    public void LeavesOutUndrawnMeshes()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.WithComponent("Quad", new Undrawn());

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(EmptyPixel, "the quad is set aside");
    }

    [Fact]
    public void ScalesTheAmbientLightInsideIndoorVolumes()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Room", "components": { "transform": { "position": [0, -2, 0] }, "indoorVolume": { "size": [4, 4, 4], "ambient": 0.5 } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0.25f);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[0]);
        red.Should().BeInRange(30, 34, "half the ambient light reaches inside");
    }

    [Fact]
    public void CutsAwayWhatStandsInFrontOfTheCutaway()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithModel("Blue.glb", static model => model.WithQuad(1).WithBaseColor(new Vector4(0, 0, 1, 1)));
        _builder.WithScene(
            Camera,
            Quad,
            """{ "name": "Ceiling", "components": { "transform": { "position": [0, 0, 1] }, "meshRenderer": { "mesh": "Blue.glb" } } }""",
            """{ "name": "Room", "components": { "transform": { "position": [0, -1.5, 0] }, "indoorVolume": { "size": [3, 3, 1], "ambient": 1 } } }""");
        _builder.WithComponent("Room", new Cutaway());

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(RedPixel, "the room shows through the ceiling in front of it");
    }

    [Fact]
    public void KeepsUncutMeshesInFrontOfTheCutaway()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithModel("Blue.glb", static model => model.WithQuad(1).WithBaseColor(new Vector4(0, 0, 1, 1)));
        _builder.WithScene(
            Camera,
            Quad,
            """{ "name": "Door", "components": { "transform": { "position": [0, 0, 1] }, "meshRenderer": { "mesh": "Blue.glb" }, "uncut": {} } }""",
            """{ "name": "Room", "components": { "transform": { "position": [0, -1.5, 0] }, "indoorVolume": { "size": [3, 3, 1], "ambient": 1 } } }""");
        _builder.WithComponent("Room", new Cutaway());

        uint[] pixels = _builder.Render();

        (byte red, _, byte blue) = SceneRendererTestsBuilder.Channels(pixels[0]);
        blue.Should().BeGreaterThan(red, "the door stays in front of the room, so the way in and out shows");
    }

    [Fact]
    public void MutesTheWorldOutsideTheCutaway()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Room", "components": { "transform": { "position": [5, 0, 0] }, "indoorVolume": { "ambient": 1, "outsideMuted": 1 } } }""");
        _builder.WithComponent("Room", new Cutaway());

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[0]);
        green.Should().Be(red, "the world outside the room is greyed");
        blue.Should().Be(red);
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
        _builder.WithProbes("Room.fvprobes", probes);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Volume", "components": { "transform": {}, "irradianceVolume": { "size": [4, 4, 4], "probesX": 2, "probesY": 2, "probesZ": 2, "probes": "Room.fvprobes" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(126, 129, "the probes give every surface half the light");
    }

    [Fact]
    public void LeavesProbesOutWhileTheyAreSwitchedOff()
    {
        IrradianceProbes probes = new IrradianceProbesTestsBuilder().WithEvenIrradiance(new Vector3(0.5f, 0.5f, 0.5f)).Build();
        _builder.WithProbes("Room.fvprobes", probes);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Volume", "components": { "transform": {}, "irradianceVolume": { "size": [4, 4, 4], "probesX": 2, "probesY": 2, "probesZ": 2, "probes": "Room.fvprobes" } } }""");
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.WithRenderer(static renderer => renderer.Probes = false);

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(BlackPixel);
    }

    [Fact]
    public void LeavesMeshesOutsideTheIrradianceVolumeToTheOtherLights()
    {
        IrradianceProbes probes = new IrradianceProbesTestsBuilder().WithEvenIrradiance(new Vector3(0.5f, 0.5f, 0.5f)).Build();
        _builder.WithProbes("Room.fvprobes", probes);
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad, """{ "name": "Volume", "components": { "transform": { "position": [10, 0, 0] }, "irradianceVolume": { "size": [4, 4, 4], "probesX": 2, "probesY": 2, "probesZ": 2, "probes": "Room.fvprobes" } } }""");
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
    public void DarkensWhatStandsNearerThanTheFocus()
    {
        const string main = """{ "name": "Main", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 }, "mainCamera": { "nearStart": 0, "nearEnd": 1, "nearAmount": 0.5, "farAmount": 0 } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(main, Quad);
        _builder.WithRenderer(static renderer => renderer.Focus = new Vector3(0, 0, -5));

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(126, 129, "the quad stands five units in front of the focus, past the near end");
        green.Should().Be(0);
        blue.Should().Be(0);
        pixels[0].Should().Be(EmptyPixel, "nothing is painted there to darken");
    }

    [Fact]
    public void MeltsWhatLiesFartherThanTheFocusIntoTheFarColour()
    {
        const string main = """{ "name": "Main", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 }, "mainCamera": { "nearAmount": 0, "farStart": 0, "farEnd": 1, "farAmount": 0.5, "farColor": [0, 0, 1] } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(main, Quad);
        _builder.WithRenderer(static renderer => renderer.Focus = new Vector3(0, 0, 3));

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(126, 129, "the quad lies three units behind the focus, past the far end");
        green.Should().Be(0);
        blue.Should().BeInRange(126, 129);
        pixels[0].Should().Be(EmptyPixel);
    }

    [Fact]
    public void MeasuresTheDepthFadeFromTheCameraWithoutAFocus()
    {
        const string main = """{ "name": "Main", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 }, "mainCamera": { "nearAmount": 0, "farStart": 4, "farEnd": 4.5, "farAmount": 1, "farColor": [0, 0, 0] } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(main, Quad);

        uint[] pixels = _builder.Render();

        pixels[5].Should().Be(BlackPixel, "the quad is five units from the camera, past the far end");
    }

    [Fact]
    public void LeavesTheDepthFadeOutWhileItIsSwitchedOff()
    {
        const string main = """{ "name": "Main", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 }, "mainCamera": { "farStart": 0, "farEnd": 1, "farAmount": 1, "farColor": [0, 0, 0] } } }""";
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(main, Quad);
        _builder.WithRenderer(static renderer => renderer.DepthFades = false);

        uint[] pixels = _builder.Render();

        pixels[5].Should().Be(RedPixel);
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

        withoutBloom[0].Should().Be(EmptyPixel);
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
    public void LaysThePaperUnderThePaintShowingThroughWhereItIsThin()
    {
        uint grey = SceneRendererTestsBuilder.Rgba(128, 128, 128, 255);
        _builder.WithTexture("Paper.tga", 1, 1, [grey]);
        _builder.WithFile("Paper.fvcanvas", """{ "image": "Paper.tga" }""");
        _builder.WithMood(static mood => mood with { Canvas = "Paper.fvcanvas" });
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);

        uint[] thick = _builder.Render();
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(new Vector4(1, 0, 0, 0.5f)));
        _builder.NotifyChanged("Quad.fvmodel");
        uint[] thin = _builder.Render();

        SceneRendererTestsBuilder.Channels(thick[5]).Should().Be(((byte)255, (byte)0, (byte)0), "thick paint hides the paper");
        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(thin[5]);
        green.Should().BeInRange(25, 31, "half of the paper's light (sRGB 128 is about a fifth) shows through half-thick paint");
        blue.Should().Be(green);
        red.Should().BeInRange(150, 160);
    }

    [Fact]
    public void ShowsThePaperUnlitWhereNothingIsPainted()
    {
        uint grey = SceneRendererTestsBuilder.Rgba(128, 128, 128, 255);
        _builder.WithTexture("Paper.tga", 1, 1, [grey]);
        _builder.WithFile("Paper.fvcanvas", """{ "image": "Paper.tga" }""");
        _builder.WithMood(static mood => mood with { Canvas = "Paper.fvcanvas", Exposure = 3 });
        _builder.WithScene(Camera);

        uint[] pixels = _builder.Render();

        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(53, 57, "the paper keeps its own colour whatever the light and grading");
        green.Should().Be(red);
        blue.Should().Be(red);
    }

    [Fact]
    public void ShowsTheCanvasThroughPlainPaint()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(new Vector4(0.3f, 0.3f, 0.3f, 1)));

        uint[] pixels = RenderPaper(_builder, Camera, Quad);

        uint[] painted = [pixels[5], pixels[6], pixels[9], pixels[10]];
        painted.Distinct().Should().HaveCountGreaterThan(1, "the canvas's weave shows through paint without impasto");
    }

    [Fact]
    public void CoversTheCanvasWithThickImpasto()
    {
        uint white = SceneRendererTestsBuilder.Rgba(255, 255, 255, 255);
        uint[] high = [white, white, white, white];
        _builder.WithTexture("Wall.reb", 1, 1, [SceneRendererTestsBuilder.Rgba(255, 0, 0, 255)]);
        _builder.WithTexture("Wall.relief.dds", 2, 2, high, PixelFormat.Rgba8UNorm);
        _builder.WithFile("Wall.fvmat", """{ "parameters": { "texture": "Wall.reb", "reliefDepth": 0 } }""");
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Vector4.One));

        uint[] pixels = RenderPaper(_builder, Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Wall.fvmat"] } } }""");

        uint[] painted = [pixels[5], pixels[6], pixels[9], pixels[10]];
        painted.Should().AllBeEquivalentTo(RedPixel, "thick impasto hides the canvas");
    }

    [Fact]
    public void KeepsThePaperStillWhenTheCameraTurnsInPlace()
    {
        const string turned = """
            { "name": "Camera", "components": { "transform": { "position": [0, 0, 0], "rotation": [0, 0.38268343, 0, 0.9238795] }, "camera": { "orthographicHeight": 2 } } }
            """;
        using SceneRendererTestsBuilder other = new();

        const string Ahead = """
            { "name": "Camera", "components": { "transform": { "position": [0, 0, 0] }, "camera": { "orthographicHeight": 2 } } }
            """;

        uint[] ahead = RenderPaper(_builder, Ahead);
        uint[] aside = RenderPaper(other, turned);

        aside.Should().Equal(ahead, "the canvas is flat, so a camera turning where it stands leaves it where it is");
    }

    [Fact]
    public void ScalesThePaperWithAnOrthographicZoom()
    {
        const string zoomed = """
            { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 3 } } }
            """;
        using SceneRendererTestsBuilder other = new();

        uint[] near = RenderPaper(_builder, Camera);
        uint[] far = RenderPaper(other, zoomed);

        far.Should().NotEqual(near, "the canvas grows and shrinks with the world as the camera zooms");
    }

    [Fact]
    public void MovesThePaperWithAnOrthographicPan()
    {
        const string panned = """
            { "name": "Camera", "components": { "transform": { "position": [0.5, 0, 5] }, "camera": { "orthographicHeight": 2 } } }
            """;
        using SceneRendererTestsBuilder other = new();

        uint[] still = RenderPaper(_builder, Camera);
        uint[] moved = RenderPaper(other, panned);

        moved.Should().NotEqual(still, "the paper slides with the world as the camera pans");
    }

    [Fact]
    public void FlattensTheCanvasToItsAverageAtNoIntensity()
    {
        uint[] flat = RenderCanvas(_builder, """{ "image": "Paper.tga", "intensity": 0 }""", Camera);

        flat.Distinct().Should().ContainSingle("without intensity the canvas is its average colour alone");
    }

    [Fact]
    public void PaintsAMaterialsTextureInPlaceOfThePartsOwn()
    {
        uint blue = SceneRendererTestsBuilder.Rgba(0, 0, 255, 255);
        _builder.WithTexture("Blue.tga", 1, 1, [blue]);
        _builder.WithFile("Blue.fvmat", """{ "parameters": { "texture": "Blue.tga" } }""");
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Vector4.One));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Blue.fvmat"] } } }""");

        uint[] pixels = _builder.Render();

        (byte red, _, byte blueChannel) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeLessThan(10);
        blueChannel.Should().BeGreaterThan(200);
    }

    [Fact]
    public void TiltsTheLightByAPaintingsRelief()
    {
        uint white = SceneRendererTestsBuilder.Rgba(255, 255, 255, 255);
        uint[] rising = [SceneRendererTestsBuilder.Rgba(0, 0, 0, 255), SceneRendererTestsBuilder.Rgba(85, 85, 85, 255), SceneRendererTestsBuilder.Rgba(170, 170, 170, 255), white];
        _builder.WithTexture("Wall.reb", 1, 1, [white]);
        _builder.WithTexture("Wall.relief.dds", 4, 1, rising, PixelFormat.Rgba8UNorm);
        _builder.WithFile("Wall.fvmat", """{ "parameters": { "texture": "Wall.reb", "reliefDepth": 0 } }""");
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Vector4.One));
        _builder.WithLight(sunDirection: new Vector3(-1, 0, -1), ambient: 0);
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Wall.fvmat"] } } }""");
        uint[] flat = _builder.Render();
        _builder.WithFile("Wall.fvmat", """{ "parameters": { "texture": "Wall.reb", "reliefDepth": 0.1 } }""");
        _builder.NotifyChanged("Wall.fvmat");

        uint[] raised = _builder.Render();

        (byte flatRed, _, _) = SceneRendererTestsBuilder.Channels(flat[5]);
        (byte raisedRed, _, _) = SceneRendererTestsBuilder.Channels(raised[5]);
        raisedRed.Should().BeLessThan((byte)(flatRed - 10), "the paint rises toward the sun, so its slope faces away from it");
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
        _builder.NotifyChanged("Quad.fvmodel");
        uint[] pixels = _builder.Render();

        uint bluePixel = SceneRendererTestsBuilder.Rgba(0, 0, 255, 255);
        pixels.Should().AllBeEquivalentTo(bluePixel);
    }

    [Fact]
    public void DrawsTheMeshesAModuleMakes()
    {
        _builder.WithFile("Van.fvtest", "4");
        _builder.WithMeshSource(new TestMeshSource());
        _builder.WithScene(Camera, VanBody);

        uint[] pixels = _builder.Render();

        pixels.Should().OnlyContain(pixel => pixel != EmptyPixel && pixel != BlackPixel, "the box covers the whole view");
        (byte red, byte green, byte blue) = SceneRendererTestsBuilder.Channels(pixels[5]);
        green.Should().Be(red, "a made solid is grey");
        blue.Should().Be(red);
    }

    [Fact]
    public void MakesAMeshAgainWhenItsFileChanges()
    {
        _builder.WithFile("Van.fvtest", "1");
        _builder.WithMeshSource(new TestMeshSource());
        _builder.WithScene(Camera, VanBody);
        uint[] small = _builder.Render();

        _builder.WithFile("Van.fvtest", "4");
        _builder.NotifyChanged("Van.fvtest");
        uint[] large = _builder.Render();

        small.Should().AllBeEquivalentTo(EmptyPixel, "a box one metre high stands below the view");
        large.Should().OnlyContain(pixel => pixel != EmptyPixel, "the box made again covers the view");
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
        _builder.WithFile("Glow.fvmat", """{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0] } }""");
        _builder.WithScene(Camera, MaterialQuad("Glow.fvmat"));
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        uint green = SceneRendererTestsBuilder.Rgba(0, 255, 0, 255);
        pixels.Should().AllBeEquivalentTo(green, "a black tint hides the red base color and the emissive light shows alone");
    }

    [Fact]
    public void WrapsDiffuseLightRoundToSurfacesTheLightGrazes()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Wrapped.fvmat", """{ "parameters": { "wrap": 1 } }""");
        _builder.WithScene(Camera, MaterialQuad("Wrapped.fvmat"));
        _builder.WithLight(sunDirection: -Vector3.UnitX, ambient: 0);

        uint[] pixels = _builder.Render();

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        red.Should().BeInRange(124, 132, "full wrap lights a surface the light grazes by half");
    }

    [Fact]
    public void TintsTheSidesTurnedFromTheLight()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Vector4.One));
        _builder.WithFile("Cool.fvmat", """{ "parameters": { "shadowTint": [0, 0, 0.5] } }""");
        _builder.WithScene(Camera, MaterialQuad("Cool.fvmat"));
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
        _builder.WithFile("Shiny.fvmat", """{ "parameters": { "specular": 0.5 } }""");
        _builder.WithScene(Camera, MaterialQuad("Shiny.fvmat"));
        _builder.WithLight(sunDirection: -Vector3.UnitZ, ambient: 0);

        uint[] pixels = _builder.Render();

        (_, byte green, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        green.Should().BeInRange(124, 132, "a mirror-facing surface reflects the specular share of white sunlight");
    }

    [Fact]
    public void KeepsTheModelsLookWithoutAMaterial()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Plain.fvmat", """{ "parameters": {} }""");
        _builder.WithScene(Camera, MaterialQuad("Plain.fvmat"));

        uint[] pixels = _builder.Render();

        pixels.Should().AllBeEquivalentTo(RedPixel, "every parameter defaults to the plain look");
    }

    [Fact]
    public void RedrawsAMaterialChangedOnDisk()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        _builder.WithFile("Glow.fvmat", """{ "parameters": { "emissive": [0, 0, 0] } }""");
        _builder.WithScene(Camera, MaterialQuad("Glow.fvmat"));
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.Render();

        _builder.WithFile("Glow.fvmat", """{ "parameters": { "emissive": [0, 0, 1] } }""");
        _builder.NotifyChanged("Glow.fvmat");
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

    [Fact]
    public void DrawsALineOfTheOutlineColorAroundOutlinedMeshes()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb" }, "outline": { "color": [0, 1, 0] } } }""");
        _builder.WithTargetSize(8);

        uint[] pixels = _builder.Render();

        uint green = SceneRendererTestsBuilder.Rgba(0, 255, 0, 255);
        int row = 3 * 8;
        pixels[row].Should().Be(green, "the line is two pixels wide");
        pixels[row + 1].Should().Be(green);
        pixels[row + 3].Should().Be(RedPixel, "the mesh itself keeps its look");
        pixels[row + 6].Should().Be(green);
        pixels[0].Should().Be(EmptyPixel, "the corner is further than two pixels from the mesh");
    }

    [Fact]
    public void DrawsDoorwaysAsOutlinesAlone()
    {
        _builder.WithScene(Camera, """{ "name": "Doorway", "components": { "transform": { "position": [0, -0.5, 0] }, "doorway": { "size": [1, 1, 0.1] } } }""");
        _builder.WithTargetSize(8);

        uint[] pixels = _builder.Render();

        uint yellow = SceneRendererTestsBuilder.Rgba(255, 255, 0, 255);
        int row = 3 * 8;
        pixels[row].Should().Be(yellow, "a doorway is outlined in yellow by default");
        pixels[row + 3].Should().Be(EmptyPixel, "the doorway's box itself is never drawn");
    }

    [Fact]
    public void LeavesMeshesWithoutAnOutlineUnlined()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithScene(Camera, Quad);
        _builder.WithTargetSize(8);

        uint[] pixels = _builder.Render();

        pixels[3 * 8].Should().Be(EmptyPixel);
        pixels[(3 * 8) + 1].Should().Be(EmptyPixel);
    }

    /// <summary>A paper of sixteen different greys under the entities, a camera first.</summary>
    private static uint[] RenderPaper(SceneRendererTestsBuilder builder, params string[] entities)
    {
        return RenderCanvas(builder, """{ "image": "Paper.tga" }""", entities);
    }

    /// <summary>A paper of sixteen different greys, set up by the canvas file, under the entities, a camera first.</summary>
    private static uint[] RenderCanvas(SceneRendererTestsBuilder builder, string canvas, params string[] entities)
    {
        uint[] texels = new uint[16];
        for (int index = 0; index < texels.Length; index++)
        {
            byte value = (byte)(40 + index * 13);
            texels[index] = SceneRendererTestsBuilder.Rgba(value, value, value, 255);
        }

        builder.WithTexture("Paper.tga", 4, 4, texels, mips: true);
        builder.WithFile("Paper.fvcanvas", canvas);
        builder.WithMood(static mood => mood with { Canvas = "Paper.fvcanvas" });
        builder.WithScene(entities);
        return builder.Render();
    }
}
