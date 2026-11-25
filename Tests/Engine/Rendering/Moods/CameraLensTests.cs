using System.Numerics;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

public sealed class CameraLensTests : IDisposable
{
    private const string Main = """
        {
          "$schema": ".schemas/lens.schema.json",
          "bloom": 0.2,
          "tonemapper": "Aces",
          "depthNearAmount": 0.3,
          "depthFarColor": [0.78, 0.74, 0.68]
        }
        """;

    private readonly CameraLensTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HoldsOnlyTheValuesItsFileNames()
    {
        CameraLens lens = _builder.WithFile("Main.fvlens", Main).Load("Main.fvlens");
        LightingMood mood = new() { Exposure = 0.8f };

        lens.Values.Apply(mood);

        mood.Bloom.Should().Be(0.2f);
        mood.Tonemapper.Should().Be(Tonemapper.Aces);
        mood.DepthNearAmount.Should().Be(0.3f);
        mood.DepthFarColor.Should().Be(new Vector3(0.78f, 0.74f, 0.68f));
        mood.Exposure.Should().Be(0.8f, "the exposure is the environment's");
    }

    [Fact]
    public void RefusesAValueOfTheEnvironment()
    {
        _builder.WithFile("Main.fvlens", """{ "exposure": 1.2 }""");

        Action load = () => _builder.Load("Main.fvlens");

        load.Should().Throw<InvalidDataException>().WithMessage("*'exposure' belongs to the environment*");
    }

    [Fact]
    public void RefusesANameThatIsNoMoodValue()
    {
        _builder.WithFile("Main.fvlens", """{ "blooom": 0.2 }""");

        Action load = () => _builder.Load("Main.fvlens");

        load.Should().Throw<InvalidDataException>().WithMessage("*'blooom'*");
    }

    [Fact]
    public void IsCheckedWithAGamesContent()
    {
        _builder
            .WithFile("Main.fvlens", Main)
            .WithFile("Cellar.fvlens", """{ "fogDensity": 0.3 }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().ContainSingle();
        problems[0].Path.Should().Be("Cellar.fvlens");
        problems[0].Message.Should().Contain("fogDensity");
    }

    [Fact]
    public void DescribesOnlyTheLensValuesInItsSchema()
    {
        string written = _builder.WriteSchema();

        JsonObject properties = JsonNode.Parse(written)!["properties"]!.AsObject();
        properties.Should().ContainKey("bloom");
        properties.Should().ContainKey("depthFarColor");
        properties.Should().ContainKey("$schema");
        properties.Should().NotContainKey("exposure");
    }

    [Fact]
    public void IsNamedByTheMainCamera()
    {
        _builder.WithMainCamera("Cameras/Main.fvlens");

        MainCamera.FindLens(_builder.World).Should().Be("Cameras/Main.fvlens");
    }

    [Fact]
    public void IsNoneWithoutAMainCameraOrALensNamed()
    {
        MainCamera.FindLens(_builder.World).Should().BeNull();

        _builder.WithMainCamera(string.Empty);

        MainCamera.FindLens(_builder.World).Should().BeNull();
    }
}
