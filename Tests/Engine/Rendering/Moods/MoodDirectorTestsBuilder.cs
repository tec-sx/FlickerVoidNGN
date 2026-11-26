using System.Text.Json;

using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Rendering.Sky;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

/// <summary>Builds a mood director, and the overrides and environments laid on it from their files' JSON.</summary>
internal sealed class MoodDirectorTestsBuilder
{
    /// <summary>An environment whose exposure climbs evenly from 0.4 at midnight to 1.2 at noon and back.</summary>
    public const string RisingExposure = """
        { "numbers": { "exposure": { "keys": [ { "time": 0, "value": 0.4, "mode": "Linear" }, { "time": 12, "value": 1.2, "mode": "Linear" } ] } } }
        """;

    private readonly MoodDirector _director = new();

    public static MoodOverride Mood(string json)
    {
        return JsonSerializer.Deserialize(json, RenderingJsonContext.Default.MoodOverride)!;
    }

    public static LightingEnvironment Environment(string json)
    {
        return JsonSerializer.Deserialize(json, RenderingJsonContext.Default.LightingEnvironment)!;
    }

    public MoodDirectorTestsBuilder WithEnvironment(string json)
    {
        LightingEnvironment environment = Environment(json);
        _director.SetEnvironment(environment, 0);
        return this;
    }

    public MoodDirectorTestsBuilder WithSunPath(SunPath path)
    {
        _director.SetSunPath(path);
        return this;
    }

    public MoodDirectorTestsBuilder WithLayer(MoodLayer layer, string json)
    {
        MoodOverride mood = Mood(json);
        _director.SetLayer(layer, mood, 0);
        return this;
    }

    /// <summary>The director after its first frame, at midnight, so what changes next blends.</summary>
    public MoodDirector BuildStarted()
    {
        _director.Update(0, 0);
        return _director;
    }

    public MoodDirector Build() => _director;
}
