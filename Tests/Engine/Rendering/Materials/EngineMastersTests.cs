using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Engine.Rendering.Tests.Materials;

/// <summary>The engine's masters and starter instances, drawn by the scene renderer on a quad the camera looks at.</summary>
public sealed class EngineMastersTests : IDisposable
{
    private const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 } } }
        """;

    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly SceneRendererTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void LightsMetalWithLessDiffuseLightThanPaint()
    {
        _builder.WithFile("Painted.fvmat", """{ "parent": "Engine/Rendering/Masters/Painted.fvmaster" }""");
        _builder.WithFile("Metal.fvmat", """{ "parent": "Engine/Rendering/Masters/Metal.fvmaster", "parameters": { "metalness": 1 } }""");

        (byte painted, byte metal) = RenderSideBySide("Painted.fvmat", "Metal.fvmat");

        painted.Should().BeGreaterThan(200);
        metal.Should().BeLessThan(40, "bare metal takes no diffuse light, and without a highlight reflects none");
    }

    [Fact]
    public void LightsGlowingPaintWithoutLight()
    {
        _builder.WithLight(sunDirection: Vector3.UnitZ, ambient: 0);
        _builder.WithFile("Painted.fvmat", """{ "parent": "Engine/Rendering/Masters/Painted.fvmaster" }""");
        _builder.WithFile("Neon.fvmat", """{ "parent": "Engine/Rendering/Masters/Glow.fvmaster", "parameters": { "glow": 2, "lit": 0 } }""");

        (byte painted, byte glowing) = RenderSideBySide("Painted.fvmat", "Neon.fvmat");

        painted.Should().BeLessThan(10);
        glowing.Should().BeGreaterThan(200);
    }

    [Fact]
    public void LightsSkinPastTheTerminatorAsLightScattersUnderIt()
    {
        Vector3 grazing = Vector3.Normalize(new Vector3(-1, 0, 0.05f));
        _builder.WithLight(sunDirection: grazing, ambient: 0);
        _builder.WithFile("Painted.fvmat", """{ "parent": "Engine/Rendering/Masters/Painted.fvmaster" }""");
        _builder.WithFile("Skin.fvmat", """{ "parent": "Engine/Rendering/Masters/Skin.fvmaster", "parameters": { "scatter": [1, 0.3, 0.1], "scatterWidth": 1 } }""");

        (byte painted, byte skin) = RenderSideBySide("Painted.fvmat", "Skin.fvmat");

        painted.Should().BeLessThan(10, "the sun is behind the paint's terminator");
        skin.Should().BeGreaterThan(25, "red light scatters round the terminator of skin");
    }

    [Fact]
    public void DrawsTheBackOfATwoSidedMastersCards()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithBuiltContent("Engine/Rendering/Masters");
        _builder.WithFile("Painted.fvmat", """{ "parent": "Engine/Rendering/Masters/Painted.fvmaster" }""");
        _builder.WithFile("Leaves.fvmat", """{ "parent": "Engine/Rendering/Masters/Cutout.fvmaster" }""");
        _builder.WithScene(
            Camera,
            """{ "name": "Painted", "components": { "transform": { "position": [-0.5, 0, 0], "rotation": [0, 1, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Painted.fvmat"] } } }""",
            """{ "name": "Leaves", "components": { "transform": { "position": [0.5, 0, 0], "rotation": [0, 1, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Leaves.fvmat"] } } }""");

        uint[] pixels = _builder.Render();

        (byte painted, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        (byte leaves, _, _) = SceneRendererTestsBuilder.Channels(pixels[6]);
        painted.Should().Be(0, "a one-sided surface turned away is culled");
        leaves.Should().BeGreaterThan(200, "a two-sided card's back is drawn and lit as it faces the camera");
    }

    [Fact]
    public void LaysTheSurfacesLayerOverItsPainting()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithBuiltContent("Engine/Rendering/Masters");
        _builder.WithFile("Grimy.fvmat", """{ "parent": "Engine/Rendering/Masters/Surface.fvmaster", "parameters": { "layerTint": [0, 0, 1], "layerAmount": 1 } }""");
        _builder.WithScene(Camera, """{ "name": "Quad", "components": { "transform": {}, "meshRenderer": { "mesh": "Quad.glb", "materials": ["Grimy.fvmat"] } } }""");

        uint[] albedo = _builder.RenderTexels(static gBuffer => gBuffer.Albedo);

        albedo[5].Should().Be(SceneRendererTestsBuilder.Rgba(0, 0, 255, 255));
    }

    [Fact]
    public void PacksEveryStarterInstanceWithItsMaster()
    {
        string content = Path.Combine(AppContext.BaseDirectory, "Content");
        string folder = Path.Combine(content, "Engine", "Rendering", "Materials");
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Rendering");
        IReadOnlyList<ShaderParameter> scene = ShaderParameters.Load(shaders, "Scene").Parameters;
        string[] files = Directory.GetFiles(folder, "*.fvmat");

        files.Should().Contain(file => file.EndsWith("Drywall.fvmat", StringComparison.Ordinal))
            .And.Contain(file => file.EndsWith("Brick.fvmat", StringComparison.Ordinal))
            .And.Contain(file => file.EndsWith("Rubber.fvmat", StringComparison.Ordinal))
            .And.Contain(file => file.EndsWith("Skin.fvmat", StringComparison.Ordinal));
        foreach (string file in files)
        {
            MaterialFile material = ReadFile(file, RenderingJsonContext.Default.MaterialFile);
            string masterPath = Path.Combine(content, material.Parent!);
            MasterFile master = ReadFile(masterPath, RenderingJsonContext.Default.MasterFile);

            Action pack = () => MaterialLayout.PackInstance(scene, master, material);

            pack.Should().NotThrow($"{Path.GetFileName(file)} names only its master's and the scene shader's parameters");
        }
    }

    [Fact]
    public void ReadsTheShadingModelWhereMaterialHlslLoadsIt()
    {
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Rendering");
        IReadOnlyList<ShaderParameter> scene = ShaderParameters.Load(shaders, "Scene").Parameters;

        int size = MaterialLayout.Size(scene);

        size.Should().Be(96, "Material.hlsl loads the shading model at 96 and its values at 100");
    }

    private static T ReadFile<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        byte[] json = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize(json, typeInfo)!;
    }

    /// <summary>The red of the lit scene where each of two red quads stands, the first on the left, the second on the right.</summary>
    private (byte Left, byte Right) RenderSideBySide(string left, string right)
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(0.5f).WithBaseColor(Red));
        _builder.WithBuiltContent("Engine/Rendering/Masters");
        _builder.WithScene(
            Camera,
            $$"""{ "name": "Left", "components": { "transform": { "position": [-0.5, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb", "materials": ["{{left}}"] } } }""",
            $$"""{ "name": "Right", "components": { "transform": { "position": [0.5, 0, 0] }, "meshRenderer": { "mesh": "Quad.glb", "materials": ["{{right}}"] } } }""");
        uint[] pixels = _builder.Render();
        (byte leftRed, _, _) = SceneRendererTestsBuilder.Channels(pixels[5]);
        (byte rightRed, _, _) = SceneRendererTestsBuilder.Channels(pixels[6]);
        return (leftRed, rightRed);
    }
}
