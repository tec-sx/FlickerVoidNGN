using System.Text;

using FlickerVoid.Editor.Assets.Moods;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Editor.Assets.Tests.Moods;

public sealed class MoodDocumentTests : IDisposable
{
    private readonly MoodDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ReadsTheMoodFile()
    {
        MoodDocument document = _builder.Build();

        document.Path.Should().Be(MoodDocumentTestsBuilder.Night);
        document.Mood.Exposure.Should().Be(0.5f);
    }

    [Fact]
    public void HoldsEditsUnsavedUntilTheyAreSaved()
    {
        MoodDocument document = _builder.Build();

        document.Mood.Exposure = 2;
        document.Hold();

        _builder.Content.IsUnsaved(MoodDocumentTestsBuilder.Night).Should().BeTrue();
        _builder.Load().Exposure.Should().Be(2);
        _builder.ReadFile().Should().NotContain("\"exposure\": 2");
        _builder.Content.Save(MoodDocumentTestsBuilder.Night);
        _builder.ReadFile().Should().Contain("\"exposure\": 2");
    }

    [Fact]
    public void KeepsTheSchemaTheFileNames()
    {
        MoodDocument document = _builder.Build();

        document.Hold();
        _builder.Content.Save(MoodDocumentTestsBuilder.Night);

        _builder.ReadFile().Should().Contain("\"$schema\": \"../.schemas/mood.schema.json\"");
    }

    [Fact]
    public void EditsAMoodOfItsOwnNotOneLoadedBefore()
    {
        LightingMood loaded = _builder.Load();
        MoodDocument document = _builder.Build();

        document.Mood.Exposure = 3;

        document.Mood.Should().NotBeSameAs(loaded);
        loaded.Exposure.Should().Be(0.5f);
    }

    [Fact]
    public void ShowsTheUnsavedEditsTheFileAlreadyHolds()
    {
        byte[] edited = Encoding.UTF8.GetBytes("""{ "exposure": 4 }""");
        _builder.Content.Edit(MoodDocumentTestsBuilder.Night, edited);

        MoodDocument document = _builder.Build();

        document.Mood.Exposure.Should().Be(4);
    }

    [Fact]
    public void WritesOnlyTheValuesTheFileNamedAndTheOnesEdited()
    {
        MoodDocument document = _builder.Build();

        document.Mood.FogDensity = 0.3f;
        document.Hold();
        _builder.Content.Save(MoodDocumentTestsBuilder.Night);

        string file = _builder.ReadFile();
        file.Should().Contain("\"exposure\": 0.5");
        file.Should().Contain("\"fogDensity\": 0.3");
        file.Should().NotContain("bloom", "a value never named or edited stays out, so what lies under the mood shows");
    }

    [Fact]
    public void KeepsAnEditedValueWhenItGoesBackToWhatItWas()
    {
        MoodDocument document = _builder.Build();
        float before = document.Mood.FogDensity;

        document.Mood.FogDensity = 0.3f;
        document.Hold();
        document.Mood.FogDensity = before;
        document.Hold();
        _builder.Content.Save(MoodDocumentTestsBuilder.Night);

        _builder.ReadFile().Should().Contain("fogDensity", "a value set here overrides what lies under it, even at its default");
    }
}
