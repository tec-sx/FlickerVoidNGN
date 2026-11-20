using Arch.Core;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class SceneMoodTests : IDisposable
{
    private readonly SceneMoodTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ScenesCanNameTheirMood()
    {
        _builder.Game.Spawner.Components.Select(component => component.Name).Should().Contain("sceneMood");
    }

    [Fact]
    public void FindsTheMoodTheWorldNames()
    {
        _builder.Spawn("""{ "name": "Lighting", "components": { "sceneMood": { "mood": "Moods/Night.fvmood" } } }""");

        string? mood = SceneMood.Find(_builder.Game.World);

        mood.Should().Be("Moods/Night.fvmood");
    }

    [Fact]
    public void FindsNoMoodInAWorldThatNamesNone()
    {
        _builder.Spawn("""{ "name": "Lighting", "components": { "sceneMood": {} } }""");

        string? mood = SceneMood.Find(_builder.Game.World);

        mood.Should().BeNull();
    }
}
