using System.Text.Json;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

public sealed class MoodOverrideTests : IDisposable
{
    private readonly MoodOverrideTestsBuilder _builder = new();
    private readonly MoodValuesTestsBuilder _moods = new();

    [Fact]
    public void ChangesOnlyTheValuesItsFileNames()
    {
        MoodOverride dusk = _builder.Load("""{ "exposure": 1.4, "fogDensity": 0.05 }""");
        LightingMood mood = _moods.BuildChanged();
        LightingMood expected = mood with { Exposure = 1.4f, FogDensity = 0.05f };

        dusk.Apply(mood);

        mood.Should().Be(expected);
    }

    [Fact]
    public void NamesTheValuesOfItsFileInTheFilesOrder()
    {
        MoodOverride dusk = _builder.Load("""{ "tint": [1, 0.8, 0.6], "ambient": 0.2 }""");

        dusk.Named.Select(value => value.Name).Should().Equal("tint", "ambient");
    }

    [Fact]
    public void SetsAValueItsFileNamesAtItsDefault()
    {
        MoodOverride clear = _builder.Load("""{ "bloom": 0, "canvas": null }""");
        LightingMood mood = _moods.BuildChanged();

        clear.Apply(mood);

        mood.Bloom.Should().Be(0);
        mood.Canvas.Should().BeNull();
    }

    [Fact]
    public void ChangesNothingWhenItsFileNamesNothing()
    {
        MoodOverride nothing = _builder.Load("""{ "$schema": "../.schemas/mood.schema.json" }""");
        LightingMood mood = _moods.BuildChanged();

        nothing.Apply(mood);

        nothing.Named.Should().BeEmpty();
        mood.Should().Be(_moods.BuildChanged());
    }

    [Fact]
    public void RefusesAFileNamingAValueAMoodDoesNotHave()
    {
        Action load = () => _builder.Load("""{ "exposre": 1.4 }""");

        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void WritesOnlyTheValuesItNames()
    {
        MoodOverride dusk = _builder.Load("""{ "exposure": 1.4, "tint": [1, 0.8, 0.6], "canvas": null }""");

        string json = JsonSerializer.Serialize(dusk, RenderingJsonContext.Default.MoodOverride);

        json.Should().Be("""{"exposure":1.4,"tint":[1,0.8,0.6],"canvas":null}""");
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
