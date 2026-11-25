using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

public sealed class LightingEnvironmentTests : IDisposable
{
    private const float Precision = 1e-4f;

    private readonly LightingEnvironmentTestsBuilder _builder = new();

    [Fact]
    public void GivesItsKeyedValuesAtTheirHours()
    {
        LightingEnvironment environment = _builder.WithNumber("exposure", 0, 0.4f).WithNumber("exposure", 12, 1.2f).Build();
        LightingMood midnight = new();
        LightingMood noon = new();

        environment.Apply(0, midnight);
        environment.Apply(12, noon);

        midnight.Exposure.Should().BeApproximately(0.4f, Precision);
        noon.Exposure.Should().BeApproximately(1.2f, Precision);
    }

    [Fact]
    public void BlendsBetweenItsKeys()
    {
        LightingEnvironment environment = _builder
            .WithNumber("exposure", 0, 0.4f)
            .WithNumber("exposure", 12, 1.2f)
            .WithColor("tint", 0, 0.2f, 0.2f, 1)
            .WithColor("tint", 12, 1, 1, 1)
            .Build();
        LightingMood morning = new();

        environment.Apply(6, morning);

        Vector3 between = new(0.6f, 0.6f, 1);
        morning.Exposure.Should().BeApproximately(0.8f, Precision);
        Vector3.Distance(morning.Tint, between).Should().BeLessThan(Precision);
    }

    [Fact]
    public void WrapsFromTheEndOfTheDayToItsStart()
    {
        LightingEnvironment environment = _builder.WithNumber("fogDensity", 0, 0.1f).WithNumber("fogDensity", 12, 0).Build();
        LightingMood evening = new();
        LightingMood nextMorning = new();
        LightingMood morning = new();

        environment.Apply(18, evening);
        environment.Apply(30, nextMorning);
        environment.Apply(6, morning);

        evening.FogDensity.Should().BeApproximately(0.05f, Precision, "the last key leads on to the first");
        nextMorning.FogDensity.Should().BeApproximately(morning.FogDensity, Precision);
    }

    [Fact]
    public void HoldsAValueWithOneKeyAllDay()
    {
        LightingEnvironment environment = _builder.WithNumber("ambient", 9, 0.5f).Build();
        LightingMood night = new();

        environment.Apply(23, night);

        night.Ambient.Should().Be(0.5f);
    }

    [Fact]
    public void LeavesValuesWithoutACurveAlone()
    {
        LightingEnvironment environment = _builder.WithNumber("ambient", 9, 0.5f).Build();
        LightingMood mood = new MoodValuesTestsBuilder().BuildChanged();
        LightingMood expected = mood with { Ambient = 0.5f };

        environment.Apply(9, mood);

        mood.Should().Be(expected);
    }

    [Fact]
    public void ReadsItsCurvesFromAFile()
    {
        const string json = """
            {
              "$schema": "../.schemas/environment.schema.json",
              "numbers": {
                "exposure": { "keys": [ { "time": 0, "value": 0.4, "mode": "Linear" }, { "time": 12, "value": 1.2, "mode": "Linear" } ] },
              },
              "colors": {
                "fogColor": { "keys": [ { "time": 6, "color": [1, 0.8, 0.6] } ] },
              },
            }
            """;
        LightingEnvironment environment = _builder.WithFile("Street.fvenv", json).Load("Street.fvenv");
        LightingMood mood = new();

        environment.Apply(3, mood);

        Vector3 fog = new(1, 0.8f, 0.6f);
        mood.Exposure.Should().BeApproximately(0.6f, Precision);
        mood.FogColor.Should().Be(fog);
    }

    [Fact]
    public void WritesItsCurvesBack()
    {
        LightingEnvironment environment = _builder.WithNumber("ambient", 9, 0.5f).WithColor("tint", 6, 1, 0.5f, 0.25f).Build();

        string json = JsonSerializer.Serialize(environment, RenderingJsonContext.Default.LightingEnvironment);

        json.Should().Be("""{"numbers":{"ambient":{"keys":[{"time":9,"value":0.5}]}},"colors":{"tint":{"keys":[{"time":6,"color":[1,0.5,0.25]}]}}}""");
    }

    [Theory]
    [InlineData("""{ "numbers": { "fogDensty": { "keys": [] } } }""", "fogDensty")]
    [InlineData("""{ "numbers": { "bloom": { "keys": [] } } }""", "bloom")]
    [InlineData("""{ "numbers": { "tint": { "keys": [] } } }""", "tint")]
    [InlineData("""{ "colors": { "exposure": { "keys": [] } } }""", "exposure")]
    [InlineData("""{ "colors": { "sunDirection": { "keys": [] } } }""", "sunDirection")]
    public void RefusesACurveForWhatIsNotItsToDraw(string json, string name)
    {
        _builder.WithFile("Street.fvenv", json);

        Action load = () => _builder.Load("Street.fvenv");

        load.Should().Throw<InvalidDataException>().WithMessage($"*{name}*");
    }

    [Fact]
    public void DescribesItsCurvesInItsSchema()
    {
        JsonObject schema = JsonSchemas.CreateFile(RenderingJsonContext.Default.LightingEnvironment);

        JsonNode number = schema["properties"]!["numbers"]!["additionalProperties"]!;
        JsonNode color = schema["properties"]!["colors"]!["additionalProperties"]!;
        number["properties"]!["keys"]!["items"]!["properties"]!["value"].Should().NotBeNull();
        color["properties"]!["keys"]!["items"]!["properties"]!["color"].Should().NotBeNull();
    }

    [Fact]
    public void IsCheckedWithAGamesContent()
    {
        _builder
            .WithFile("Street.fvenv", """{ "numbers": { "exposure": { "keys": [ { "time": 12, "value": 1.2 } ] } } }""")
            .WithFile("Cellar.fvenv", """{ "numbers": { "exposre": { "keys": [ { "time": 12, "value": 1.2 } ] } } }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().ContainSingle();
        problems[0].Path.Should().Be("Cellar.fvenv");
        problems[0].Message.Should().Contain("exposre");
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
