using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Cameras.Tests;

/// <summary>
/// A playing game with rendering, cameras and saves in a temporary folder, for the camera tests' builders: spawns entities from
/// scene JSON, runs frames at 60 a second and reads the view the renderer would draw.
/// </summary>
internal sealed class CameraTestGame : IDisposable
{
    private readonly string _saveFolder;

    public CameraTestGame()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _saveFolder = Path.Combine(tempPath, folderName);
        Game.AddRendering();
        Director = Game.AddCameras();
        Saves = Game.AddSaves(new SaveSettings { Folder = _saveFolder });
        Game.Lifecycle.Play(Game.World);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public CameraDirector Director { get; }

    public SaveSystem Saves { get; }

    /// <summary>A context for effects and flow runs, with the game's facts and services.</summary>
    public ConditionContext Context => new(Game.Facts) { Services = Game.Services };

    /// <summary>Spawns an entity from its scene JSON: <c>{ "name": ..., "components": { ... } }</c>.</summary>
    public Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }

    /// <summary>A shot at a place on the X axis with the keys' JSON (one key at its origin by default) and any more shot settings.</summary>
    public Entity SpawnShot(string name, float x, string keys = """{ "position": [0, 0, 0] }""", string extra = "")
    {
        string json = $$"""
            { "name": "{{name}}", "components": { "transform": { "position": [{{x}}, 0, 0] }, "camera": {},
              "shot": { "keys": [ {{keys}} ] {{extra}} } } }
            """;
        return Spawn(json);
    }

    public void MoveTo(Entity entity, Vector3 position)
    {
        ref Transform transform = ref Game.World.Get<Transform>(entity);
        transform.Position = position;
    }

    public void Run(float seconds)
    {
        int frames = (int)MathF.Round(seconds * 60);
        for (int frame = 0; frame < frames; frame++)
        {
            Game.Tick(GameLoop.DefaultFixedStep);
        }
    }

    public RenderScene Extract()
    {
        RenderScene scene = new();
        WorldExtractor extractor = new();
        extractor.Extract(Game.World, scene);
        return scene;
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Dispose()
    {
        Game.Dispose();
        if (Directory.Exists(_saveFolder))
        {
            Directory.Delete(_saveFolder, recursive: true);
        }
    }
}
