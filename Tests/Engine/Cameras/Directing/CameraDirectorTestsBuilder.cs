using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Cameras.Tests.Directing;

/// <summary>Builds a playing game with rendering and cameras, spawns shots from scene JSON and runs it frame by frame at 60 frames a second.</summary>
internal sealed class CameraDirectorTestsBuilder : IDisposable
{
    public static readonly TimeSpan Frame = GameLoop.DefaultFixedStep;

    private readonly string _saveFolder;

    public CameraDirectorTestsBuilder()
    {
        string tempPath = Path.GetTempPath();
        string folderName = Path.GetRandomFileName();
        _saveFolder = Path.Combine(tempPath, folderName);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public SaveSystem Saves { get; private set; } = null!;

    /// <summary>Starts the game, with saves in a temporary folder when asked.</summary>
    public CameraDirector Build(bool saves = false)
    {
        Game.AddRendering();
        CameraDirector director = Game.AddCameras();
        if (saves)
        {
            Saves = Game.AddSaves(new SaveSettings { Folder = _saveFolder });
        }

        Game.Lifecycle.Play(Game.World);
        return director;
    }

    /// <summary>Spawns an entity from its scene JSON: <c>{ "name": ..., "components": { ... } }</c>.</summary>
    public Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }

    /// <summary>A shot at the origin with an orthographic camera and the keys' JSON, optionally the one the scene starts on.</summary>
    public Entity SpawnShot(string name, string keys, bool start = false, string extra = "")
    {
        string startJson = start ? "true" : "false";
        string json = $$"""
            { "name": "{{name}}", "components": { "transform": {}, "camera": { "orthographicHeight": 10 },
              "shot": { "start": {{startJson}}, "keys": [ {{keys}} ] {{extra}} } } }
            """;
        return Spawn(json);
    }

    /// <summary>An entity cameras can follow and look at, named and placed.</summary>
    public Entity SpawnTarget(string name, Vector3 position)
    {
        string json = $$"""
            { "name": "{{name}}", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }, "cameraTarget": {} } }
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
            Game.Tick(Frame);
        }
    }

    /// <summary>The view the world is drawn from, as the renderer extracts it.</summary>
    public RenderCamera Drawn()
    {
        RenderScene scene = new();
        WorldExtractor extractor = new();
        extractor.Extract(Game.World, scene);
        return scene.Cameras[0];
    }

    /// <summary>What the director lays over the frame, as the renderer extracts it.</summary>
    public RenderTransition Transition()
    {
        RenderScene scene = new();
        WorldExtractor extractor = new();
        extractor.Extract(Game.World, scene);
        return scene.Transition;
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
