using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Cameras.Tests.Layers;

/// <summary>
/// Builds a playing game with cameras over a content folder of camera rigs, a still shot at the origin looking down -Z with an
/// orthographic height of 10 using the rig given, and a camera target, run frame by frame at 60 frames a second.
/// </summary>
internal sealed class CameraLayersTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public CameraLayersTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Shot { get; private set; }

    public Entity Target { get; private set; }

    /// <summary>Writes a rig with the layers' JSON, named <c>Rig.fvdata</c>.</summary>
    public CameraLayersTestsBuilder WithRig(string layers)
    {
        string path = Path.Combine(_folder, "Rig.fvdata");
        File.WriteAllText(path, $$"""{ "type": "cameraRig", "layers": [ {{layers}} ] }""");
        return this;
    }

    /// <summary>Starts the game with a shot using the rig (none when none was written) and a target at the given place.</summary>
    public CameraDirector Build(Vector3 target, string shotKeys = """{ "position": [0, 0, 0] }""")
    {
        Game.AddRendering();
        CameraDirector director = Game.AddCameras();
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.Lifecycle.Play(Game.World);
        string rig = File.Exists(Path.Combine(_folder, "Rig.fvdata")) ? "Rig.fvdata" : string.Empty;
        Shot = Spawn($$"""
            { "name": "Shot", "components": { "transform": {}, "camera": { "orthographicHeight": 10 },
              "shot": { "rig": "{{rig}}", "keys": [ {{shotKeys}} ] } } }
            """);
        Target = Spawn($$"""
            { "name": "Fox", "components": { "transform": { "position": [{{target.X}}, {{target.Y}}, {{target.Z}}] }, "cameraTarget": {} } }
            """);
        return director;
    }

    public void MoveTarget(Vector3 position)
    {
        ref Transform transform = ref Game.World.Get<Transform>(Target);
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

    /// <summary>The view the world is drawn from, as the renderer extracts it.</summary>
    public RenderCamera Drawn()
    {
        RenderScene scene = new();
        WorldExtractor extractor = new();
        extractor.Extract(Game.World, scene);
        return scene.Cameras[0];
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }
}
