using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Cameras.Tests.Directing;

/// <summary>
/// Builds a playing game with moods and cameras, as the host adds them, over a temporary content folder holding the scene's
/// mood (exposure and saturation) and a dark cellar mood (exposure only), and spawns the scene's mood, a street shot the scene
/// starts on and a cellar shot that names the cellar mood. Runs at 60 frames a second.
/// </summary>
internal sealed class ShotMoodSystemTestsBuilder : IDisposable
{
    public const string SceneMoodFile = "Street.fvmood";
    public const string CellarMoodFile = "Cellar.fvmood";
    public const float SceneExposure = 1.2f;
    public const float SceneSaturation = 0.5f;
    public const float CellarExposure = 0.2f;

    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"shot-moods-{Guid.NewGuid():N}");
    private readonly ContentManager _content;

    public ShotMoodSystemTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
        _content = new ContentManager(_contentFolder);
        WithFile(SceneMoodFile, $$"""{ "exposure": {{SceneExposure}}, "saturation": {{SceneSaturation}} }""");
        WithFile(CellarMoodFile, $$"""{ "exposure": {{CellarExposure}} }""");
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public MoodSystem Moods { get; private set; } = null!;

    public CameraDirector Director { get; private set; } = null!;

    public Entity Street { get; private set; }

    public Entity Cellar { get; private set; }

    /// <summary>Starts the game on the street shot.</summary>
    public ShotMoodSystemTestsBuilder Build()
    {
        Game.AddRendering();
        Moods = Game.AddMoods(_content);
        Director = Game.AddCameras();
        Spawn($$"""{ "name": "Lighting", "components": { "sceneMood": { "mood": "{{SceneMoodFile}}" } } }""");
        Street = SpawnShot("Street", """, "start": true""");
        Cellar = SpawnShot("Cellar", $$""", "mood": "{{CellarMoodFile}}" """);
        Game.Lifecycle.Play(Game.World);
        return this;
    }

    /// <summary>Rewrites a file and reports it changed, as the content watcher would.</summary>
    public void EditFile(string path, string json)
    {
        WithFile(path, json);
        _content.NotifyChanged(path);
    }

    public void Run(float seconds)
    {
        int frames = (int)MathF.Round(seconds * 60);
        for (int frame = 0; frame < frames; frame++)
        {
            Game.Tick(GameLoop.DefaultFixedStep);
        }
    }

    public void Dispose()
    {
        Game.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }

    private void WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
    }

    private Entity SpawnShot(string name, string extra)
    {
        string json = $$"""
            { "name": "{{name}}", "components": { "transform": {}, "camera": { "orthographicHeight": 10 },
              "shot": { "keys": [ { "position": [0, 0, 0] } ] {{extra}} } } }
            """;
        return Spawn(json);
    }

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }
}
